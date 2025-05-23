﻿using Microsoft.Extensions.Logging;
using PacketDotNet;
using VpnHood.Core.Common.Messaging;
using VpnHood.Core.Packets;
using VpnHood.Core.Toolkit.Jobs;
using VpnHood.Core.Toolkit.Logging;
using VpnHood.Core.Toolkit.Utils;
using VpnHood.Core.Tunneling.ClientStreams;
using VpnHood.Core.Tunneling.DatagramMessaging;

namespace VpnHood.Core.Tunneling.Channels;

public class StreamDatagramChannel : IDatagramChannel, IJob
{
    private readonly byte[] _buffer = new byte[0xFFFF * 4];
    private readonly IClientStream _clientStream;
    private readonly DateTime _lifeTime = DateTime.MaxValue;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isCloseSent;
    private bool _isCloseReceived;
    private readonly IPPacket[] _sendingPackets = [null!];

    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;
    public JobSection JobSection { get; } = new();
    public string ChannelId { get; }
    public bool Connected { get; private set; }
    public Traffic Traffic { get; } = new();
    public DateTime LastActivityTime { get; private set; } = FastDateTime.Now;

    public bool IsStream => true;

    public StreamDatagramChannel(IClientStream clientStream, string channelId)
        : this(clientStream, channelId, Timeout.InfiniteTimeSpan)
    {
    }

    public StreamDatagramChannel(IClientStream clientStream, string channelId, TimeSpan lifespan)
    {
        ChannelId = channelId;
        _clientStream = clientStream ?? throw new ArgumentNullException(nameof(clientStream));
        if (!VhUtils.IsInfinite(lifespan)) {
            _lifeTime = FastDateTime.Now + lifespan;
            JobRunner.Default.Add(this);
        }
    }

    public void Start()
    {
        _ = StartInternal();
    }

    public async Task StartInternal()
    {
        if (_disposed)
            throw new ObjectDisposedException("StreamDatagramChannel");

        if (Connected)
            throw new Exception("StreamDatagramChannel has been already started.");

        Connected = true;
        try {
            await ReadTask(_cancellationTokenSource.Token).VhConfigureAwait();
            await SendClose().VhConfigureAwait();
        }
        catch (Exception ex) {
            VhLogger.Instance.LogError(GeneralEventId.DatagramChannel, ex, "StreamDatagramChannel has been stopped unexpectedly.");
        }
        finally {
            Connected = false;
            await DisposeAsync();
        }
    }

    // This is not thread-safe
    public Task SendPacketAsync(IPPacket packet)
    {
        _sendingPackets[0] = packet;
        return SendPacketInternalAsync(_sendingPackets);
    }

    // This is not thread-safe
    public Task SendPacketAsync(IList<IPPacket> ipPackets)
    {
        return SendPacketInternalAsync(ipPackets);
    }

    // This is not thread-safe
    private async Task SendPacketInternalAsync(IList<IPPacket> ipPackets)
    {
        if (_disposed) throw new ObjectDisposedException(VhLogger.FormatType(this));
        var cancellationToken = _cancellationTokenSource.Token;

        // check channel connectivity
        cancellationToken.ThrowIfCancellationRequested();
        if (!Connected)
            throw new Exception($"The StreamDatagramChannel is disconnected. ChannelId: {ChannelId}.");

        // copy packets to buffer
        var buffer = _buffer;
        var bufferIndex = 0;

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < ipPackets.Count; i++) {
            var ipPacket = ipPackets[i];
            var packetBytes = ipPacket.Bytes;

            // flush buffer if this packet does not fit
            if (bufferIndex > 0 && bufferIndex + packetBytes.Length > buffer.Length) {
                await _clientStream.Stream.WriteAsync(buffer, 0, bufferIndex, cancellationToken).VhConfigureAwait();
                Traffic.Sent += bufferIndex;
                bufferIndex = 0;
            }

            // Write the packet directly if it does fit in the buffer
            if (packetBytes.Length > buffer.Length) {
                // send packet
                await _clientStream.Stream.WriteAsync(packetBytes, cancellationToken).VhConfigureAwait();
                Traffic.Sent += packetBytes.Length;
            }
            else {
                Buffer.BlockCopy(packetBytes, 0, buffer, bufferIndex, packetBytes.Length);
                bufferIndex += packetBytes.Length;
            }
        }

        // send remaining buffer
        if (bufferIndex > 0) {
            await _clientStream.Stream.WriteAsync(buffer, 0, bufferIndex, cancellationToken).VhConfigureAwait();
            Traffic.Sent += bufferIndex;
        }

        LastActivityTime = FastDateTime.Now;
    }

    private async Task ReadTask(CancellationToken cancellationToken)
    {
        var stream = _clientStream.Stream;
        var eventArgs = new PacketReceivedEventArgs([]);

        await using var streamPacketReader = new StreamPacketReader(stream);
        while (!cancellationToken.IsCancellationRequested && !_isCloseReceived && !_disposed) {
            var ipPackets = await streamPacketReader.ReadAsync(cancellationToken).VhConfigureAwait();
            if (ipPackets == null)
                break;

            LastActivityTime = FastDateTime.Now;
            Traffic.Received += ipPackets.Sum(x => x.TotalLength);

            // check datagram message
            ProcessMessage(ipPackets);

            // fire new packets
            if (ipPackets.Count > 0) {
                try {
                    eventArgs.IpPackets = ipPackets;
                    PacketReceived?.Invoke(this, eventArgs);
                }
                catch (Exception ex) {
                    VhLogger.Instance.LogError(GeneralEventId.Packet, ex,
                        "Could not process the read packets. PacketCount: {PacketCount}", ipPackets.Count);
                }
            }
        }

    }

    private void ProcessMessage(IList<IPPacket> ipPackets)
    {
        // check datagram message
        List<IPPacket>? processedPackets = null;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < ipPackets.Count; i++) {
            var ipPacket = ipPackets[i];
            if (ProcessMessage(ipPacket)) {
                processedPackets ??= [];
                processedPackets.Add(ipPacket);
            }
        }

        // remove all processed packets
        if (processedPackets != null)
            foreach (var processedPacket in processedPackets) {
                ipPackets.Remove(processedPacket);
            }
    }

    private bool ProcessMessage(IPPacket ipPacket)
    {
        if (!DatagramMessageHandler.IsDatagramMessage(ipPacket))
            return false;

        var message = DatagramMessageHandler.ReadMessage(ipPacket);
        if (message is not CloseDatagramMessage)
            return false;

        _isCloseReceived = true;
        VhLogger.Instance.Log(LogLevel.Information, GeneralEventId.DatagramChannel,
            "Receiving the close message from the peer. ChannelId: {ChannelId}, Lifetime: {Lifetime}, IsCloseSent: {IsCloseSent}",
            ChannelId, _lifeTime, _isCloseSent);

        return true;
    }

    private async Task SendClose()
    {
        try {
            // already send
            if (_isCloseSent)
                return;

            _isCloseSent = true;
            _cancellationTokenSource.CancelAfter(TunnelDefaults.TcpGracefulTimeout);

            // send close message to peer
            var ipPacket = DatagramMessageHandler.CreateMessage(new CloseDatagramMessage());
            VhLogger.Instance.LogDebug(GeneralEventId.DatagramChannel,
                "StreamDatagramChannel sending the close message to the remote. ChannelId: {ChannelId}, Lifetime: {Lifetime}",
                ChannelId, _lifeTime);

            await SendPacketAsync(ipPacket);
        }
        catch (Exception ex) {
            VhLogger.LogError(GeneralEventId.DatagramChannel, ex,
                "Could not send the close message to the remote. ChannelId: {ChannelId}, Lifetime: {Lifetime}",
                ChannelId, _lifeTime);
        }
        finally {
            Connected = false;
        }
    }

    public Task RunJob()
    {
        if (Connected && FastDateTime.Now > _lifeTime) {
            VhLogger.Instance.LogDebug(GeneralEventId.DatagramChannel,
                "StreamDatagramChannel lifetime ended. ChannelId: {ChannelId}, Lifetime: {Lifetime}",
                ChannelId, _lifeTime);

            return SendClose();
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return DisposeAsync(true);
    }

    private bool _disposed;
    private readonly AsyncLock _disposeLock = new();

    public async ValueTask DisposeAsync(bool graceful)
    {
        using var lockResult = await _disposeLock.LockAsync().VhConfigureAwait();
        if (_disposed) return;

        if (graceful)
            await SendClose().VhConfigureAwait(); // this won't throw any error

        await _clientStream.DisposeAsync(graceful).VhConfigureAwait();
        _disposed = true;
    }
}
﻿using System.Collections.Concurrent;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PacketDotNet;
using VpnHood.Core.Packets;
using VpnHood.Core.Server;

namespace VpnHood.Test.Providers;

public class TestNetFilter : NetFilter
{
    private ConcurrentDictionary<Tuple<ProtocolType, IPEndPoint>, IPEndPoint> NetMap { get; } = new();
    private ConcurrentDictionary<Tuple<ProtocolType, IPEndPoint>, IPEndPoint> NetMapR { get; } = new();

    public void Init(Tuple<ProtocolType, IPEndPoint, IPEndPoint>[] items)
    {
        NetMap.Clear();
        NetMapR.Clear();

        foreach (var tuple in items) {
            Assert.IsTrue(NetMap.TryAdd(Tuple.Create(tuple.Item1, tuple.Item2), tuple.Item3));
            Assert.IsTrue(NetMapR.TryAdd(Tuple.Create(tuple.Item1, tuple.Item3), tuple.Item2));
        }
    }

    public override IPEndPoint? ProcessRequest(ProtocolType protocol, IPEndPoint requestEndPoint)
    {
        var ipEndPoint = base.ProcessRequest(protocol, requestEndPoint);
        if (ipEndPoint == null)
            return null;

        return NetMap.GetValueOrDefault(Tuple.Create(protocol, requestEndPoint), requestEndPoint);
    }

    public override IPPacket? ProcessRequest(IPPacket ipPacket)
    {
        var result = base.ProcessRequest(ipPacket);
        if (result == null) return null;
        ipPacket = result;

        switch (ipPacket.Protocol) {
            case ProtocolType.Udp: {
                var udpPacket = ipPacket.ExtractUdp();
                var newEndPoint = ProcessRequest(ipPacket.Protocol,
                    new IPEndPoint(ipPacket.DestinationAddress, udpPacket.DestinationPort));
                if (newEndPoint == null) return null;
                ipPacket.DestinationAddress = newEndPoint.Address;
                udpPacket.DestinationPort = (ushort)newEndPoint.Port;
                ipPacket.UpdateAllChecksums();
                return ipPacket;
            }
            case ProtocolType.Tcp: {
                var tcpPacket = ipPacket.ExtractTcp();
                var newEndPoint = ProcessRequest(ipPacket.Protocol,
                    new IPEndPoint(ipPacket.DestinationAddress, tcpPacket.DestinationPort));
                if (newEndPoint == null) return null;
                ipPacket.DestinationAddress = newEndPoint.Address;
                tcpPacket.DestinationPort = (ushort)newEndPoint.Port;
                ipPacket.UpdateAllChecksums();
                return ipPacket;
            }
            case ProtocolType.Icmp or ProtocolType.IcmpV6: {
                var newEndPoint = ProcessRequest(ipPacket.Protocol, new IPEndPoint(ipPacket.DestinationAddress, 0));
                if (newEndPoint == null) return null;
                ipPacket.DestinationAddress = newEndPoint.Address;
                ipPacket.UpdateAllChecksums();
                return ipPacket;
            }
            default:
                return ipPacket;
        }
    }

    public override IPPacket ProcessReply(IPPacket ipPacket)
    {
        switch (ipPacket.Protocol) {
            case ProtocolType.Udp: {
                var udpPacket = ipPacket.ExtractUdp();
                if (NetMapR.TryGetValue(
                        Tuple.Create(ipPacket.Protocol, new IPEndPoint(ipPacket.SourceAddress, udpPacket.SourcePort)),
                        out var newEndPoint1)) {
                    ipPacket.SourceAddress = newEndPoint1.Address;
                    udpPacket.SourcePort = (ushort)newEndPoint1.Port;
                    ipPacket.UpdateAllChecksums();
                }

                break;
            }

            case ProtocolType.Tcp: {
                var tcpPacket = ipPacket.ExtractTcp();
                if (NetMapR.TryGetValue(
                        Tuple.Create(ipPacket.Protocol, new IPEndPoint(ipPacket.SourceAddress, tcpPacket.SourcePort)),
                        out var tcpEndPoint)) {
                    ipPacket.SourceAddress = tcpEndPoint.Address;
                    tcpPacket.SourcePort = (ushort)tcpEndPoint.Port;
                    ipPacket.UpdateAllChecksums();
                }

                break;
            }

            case ProtocolType.Icmp or ProtocolType.IcmpV6:
                if (NetMapR.TryGetValue(
                        Tuple.Create(ipPacket.Protocol, new IPEndPoint(ipPacket.SourceAddress, 0)),
                        out var icmpEndPoint)) {
                    ipPacket.SourceAddress = icmpEndPoint.Address;
                    ipPacket.UpdateAllChecksums();
                }

                break;
        }

        return ipPacket;
    }
}
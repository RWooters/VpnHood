﻿using Ga4.Trackers;
using VpnHood.Core.Client.Abstractions;
using VpnHood.Core.Client.Services;
using VpnHood.Core.Tunneling.Factory;

namespace VpnHood.Core.Client.Device.WinDivert;

public class WinVpnService : IVpnServiceHandler, IAsyncDisposable
{
    private readonly ITracker? _tracker;
    private readonly VpnHoodService _vpnHoodService;
    public WinVpnService(
        string configFolder,
        ITracker? tracker)
    {
        _tracker = tracker;
        _vpnHoodService = new VpnHoodService(configFolder, this, new SocketFactory());
    }

    public void OnConnect()
    {
        _vpnHoodService.Connect();
    }

    public void OnDisconnect()
    {
        _vpnHoodService.Disconnect();
    }

    public ITracker? CreateTracker()
    {
        return _tracker;
    }

    public IVpnAdapter CreateAdapter()
    {
        return new WinDivertVpnAdapter();
    }

    public void ShowNotification(ConnectionInfo connectionInfo)
    {
    }

    public void StopNotification()
    {
    }

    public void StopSelf()
    {
        DisposeAsync();
    }

    public ValueTask DisposeAsync()
    {
        return _vpnHoodService.DisposeAsync();
    }
}
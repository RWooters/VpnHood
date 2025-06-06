﻿using Ga4.Trackers;
using VpnHood.Core.Server.Abstractions;
using VpnHood.Core.Server.Access.Configurations;
using VpnHood.Core.Server.SystemInformation;
using VpnHood.Core.Toolkit.Net;
using VpnHood.Core.Tunneling;
using VpnHood.Core.Tunneling.Sockets;
using VpnHood.Core.VpnAdapters.Abstractions;

namespace VpnHood.Core.Server;

public class ServerOptions
{
    public ISocketFactory SocketFactory { get; init; } = new SocketFactory();
    public ITracker? Tracker { get; init; }
    public ISystemInfoProvider? SystemInfoProvider { get; init; }
    public INetFilter NetFilter { get; init; } = new NetFilter();
    public INetConfigurationProvider? NetConfigurationProvider { get; init; }
    public ISwapMemoryProvider? SwapMemoryProvider { get; init; }
    public IVpnAdapter? VpnAdapter { get; init; }
    public bool AutoDisposeAccessManager { get; init; } = true;
    public TimeSpan ConfigureInterval { get; init; } = TimeSpan.FromSeconds(60);
    public string StoragePath { get; init; } = Directory.GetCurrentDirectory();
    public bool PublicIpDiscovery { get; init; } = true;
    public ServerConfig? Config { get; init; }
    public TimeSpan DeadSessionTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromMinutes(1);
    public IpNetwork VirtualIpNetworkV4 { get; init; } = TunnelDefaults.VirtualIpNetworkV4;
    public IpNetwork VirtualIpNetworkV6 { get; init; } = TunnelDefaults.VirtualIpNetworkV6;
}
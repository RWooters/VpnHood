﻿using System;
using System.Net;
using VpnHood.Client.Device;
using VpnHood.Tunneling.Factory;

namespace VpnHood.Client
{
    public class ClientOptions
    {
        /// <summary>
        ///     a never used ip that must be outside the machine
        /// </summary>
        public IPAddress TcpProxyLoopbackAddress { get; set; } = IPAddress.Parse("11.0.0.0");

        public IPAddress[]? DnsServers { get; set; }
        public bool AutoDisposePacketCapture { get; set; } = true;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
        public Version? Version { get; set; }
        public bool UseUdpChannel { get; set; } = false;
        public bool ExcludeLocalNetwork { get; set; } = true;
        public IpRange[]? IncludeIpRanges { get; set; }
        public IpRange[]? PacketCaptureIncludeIpRanges { get; set; }
        public SocketFactory? SocketFactory { get; set; }
        public int MaxTcpDatagramChannelCount { get; set; } = 4;
        public string? UserAgent { get; set; }

#if  DEBUG
        public int ProtocolVersion { get; set; }
#endif
    }
}
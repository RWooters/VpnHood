﻿using System.Net;
using VpnHood.Core.Common.Messaging;
using VpnHood.Core.Server.Access.Configurations;
using VpnHood.Core.Server.Access.Messaging;

namespace VpnHood.Core.Server.Access.Managers;

public interface IAccessManager : IDisposable
{
    bool IsMaintenanceMode { get; }
    Task<SessionResponseEx> Session_Create(SessionRequestEx sessionRequestEx);
    Task<SessionResponseEx> Session_Get(ulong sessionId, IPEndPoint hostEndPoint, IPAddress? clientIp);
    Task<SessionResponseEx[]> Session_GetAll();
    Task<SessionResponse> Session_AddUsage(ulong sessionId, Traffic traffic, string? adData);
    Task<Dictionary<ulong, SessionResponse>> Session_AddUsages(SessionUsage[] sessionUsages);
    Task<SessionResponse> Session_Close(ulong sessionId, Traffic traffic);
    Task<ServerCommand> Server_UpdateStatus(ServerStatus serverStatus);
    Task<ServerConfig> Server_Configure(ServerInfo serverInfo);
}
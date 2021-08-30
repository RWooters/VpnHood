﻿using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using VpnHood.Server;
using VpnHood.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Sockets;
using System.Net;
using System.IO;
using VpnHood.Server.AccessServers;
using VpnHood.Common.Messaging;
using System.Threading;

namespace VpnHood.Test
{
    [TestClass]
    public class Test_ClientServer
    {
        [TestMethod]
        public void Redirect_Server()
        {
            using var fileAccessServer = new FileAccessServer(Path.Combine(TestHelper.WorkingPath, $"AccessServer_{Guid.NewGuid()}"));
            using var testAccessServer = new TestAccessServer(fileAccessServer);

            // Create Server 1
            using var server1 = TestHelper.CreateServer(testAccessServer);
            var server1EndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), server1.TcpHostEndPoint.Port);

            // Create Server 2
            using var server2 = TestHelper.CreateServer(testAccessServer);
            var token2 = TestHelper.CreateAccessToken(fileAccessServer, server2.TcpHostEndPoint);
            testAccessServer.EmbedIoAccessServer.RedirectHostEndPoint = server1EndPoint;

            // Create Client
            using var client = TestHelper.CreateClient(token2);
            TestHelper.Test_Https();

            Assert.AreEqual(server1EndPoint, client.HostEndPoint);
        }

        [TestMethod]
        public void TcpChannel()
        {
            // Create Server
            using var fileAccessServer = TestHelper.CreateFileAccessServer();
            using var testAccessServer = new TestAccessServer(fileAccessServer);
            using var server = TestHelper.CreateServer(testAccessServer);
            var token = TestHelper.CreateAccessToken(server);

            // Create Client
            using var client = TestHelper.CreateClient(token, options: new ClientOptions { UseUdpChannel = false });

            TestTunnel(server, client);

            // check HostEndPoint in server
            fileAccessServer.SessionManager.Sessions.TryGetValue(client.SessionId, out var session);
            Assert.AreEqual(token.HostEndPoint, session?.HostEndPoint);

            // check UserAgent in server
            Assert.AreEqual(client.UserAgent, session?.ClientInfo.UserAgent);

            // check ClientPublicAddress in server
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), client.PublicAddress);
        }

        //[TestMethod]
        //public void Proxy_tunnel_udp_auto()
        //{
        //    // Create Server
        //    using var server = TestHelper.CreateServer();
        //    var token = TestHelper.CreateAccessToken(server);
        //    Assert.AreEqual(ServerState.Started, server.State);


        //    // ************
        //    // *** TEST ***: UDP doesn't work at start


        //    // ************
        //    // *** TEST ***: UDP stop working after start


        //    // Create VpnHoodConnect
        //    using var clientConnect = TestHelper.CreateClientConnect(
        //        token: token,
        //        connectOptions: new() { MaxReconnectCount = 0, ReconnectDelay = 0, /*UdpCheckThreshold = 2000*/ });
        //    Assert.AreEqual(ClientState.Connected, clientConnect.Client.State); // checkpoint

        //    // check udp is on
        //    Assert.AreEqual(true, clientConnect.Client.UseUdpChannel); // checkpoint

        //    // turn off udp on device

        //    // check udp
        //    try { Test_Udp(); } catch { } //let first try fail and wait for TcpDatagram
        //    //Thread.Sleep(UdpCheckThreshold);
        //    Test_Udp();
        //    Assert.AreEqual(false, clientConnect.Client.UseUdpChannel); // checkpoint

        //    throw new NotImplementedException();
        //}

        [TestMethod]
        public void UdpChannel_on_fly()
        {
            // Create Server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessToken(server);

            // Create Client
            using var client = TestHelper.CreateClient(token, options: new ClientOptions { UseUdpChannel = true });
            TestTunnel(server, client);

            // switch to tcp
            client.UseUdpChannel = false;
            TestTunnel(server, client);

            // switch back to udp
            client.UseUdpChannel = true;
            TestTunnel(server, client);
        }


        [TestMethod]
        public void UdpChannel()
        {
            // Create Server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessToken(server);

            // Create Client
            using var client = TestHelper.CreateClient(token, options: new ClientOptions { UseUdpChannel = true });

            TestTunnel(server, client);
        }

        private static void TestTunnel(VpnHoodServer server, VpnHoodClient client)
        {
            Assert.AreEqual(ServerState.Started, server.State);
            Assert.AreEqual(ClientState.Connected, client.State);

            // Get session
            server.SessionManager.Sessions.TryGetValue(client.SessionId, out var serverSession);
            Assert.IsNotNull(serverSession, "Could not find session in server!");

            // ************
            // *** TEST ***: TCP invalid request should not close the vpn connection
            var oldClientSentByteCount = client.SentByteCount;
            var oldClientReceivedByteCount = client.ReceivedByteCount;
            var oldServerSentByteCount = serverSession.Tunnel.SentByteCount;
            var oldServerReceivedByteCount = serverSession.Tunnel.ReceivedByteCount;

            using var httpClient = new HttpClient();
            try
            {
                var result2 = httpClient.GetStringAsync($"http://{TestHelper.TEST_NsEndPoint1}:4/").Result;
                Assert.Fail("Exception expected!");
            }
            catch { }
            Assert.AreEqual(ClientState.Connected, client.State);

            // ************
            // *** TEST ***: TCP (TLS) by quad9
            TestHelper.Test_Https();

            // check there is send data
            Assert.IsTrue(client.SentByteCount > oldClientSentByteCount + 100, "Not enough data has been sent through the client!");
            Assert.IsTrue(client.ReceivedByteCount > oldClientReceivedByteCount + 2000, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.SentByteCount > oldServerSentByteCount + 2000, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.ReceivedByteCount > oldServerReceivedByteCount + 100, "Not enough data has been sent through the client!");

            // ************
            // *** TEST ***: UDP
            oldClientSentByteCount = client.SentByteCount;
            oldClientReceivedByteCount = client.ReceivedByteCount;
            oldServerSentByteCount = serverSession.Tunnel.SentByteCount;
            oldServerReceivedByteCount = serverSession.Tunnel.ReceivedByteCount;

            TestHelper.Test_Udp();

            Assert.IsTrue(client.SentByteCount > oldClientSentByteCount + 10, "Not enough data has been sent through the client!");
            Assert.IsTrue(client.ReceivedByteCount > oldClientReceivedByteCount + 10, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.SentByteCount > oldServerSentByteCount + 10, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.ReceivedByteCount > oldServerReceivedByteCount + 10, "Not enough data has been sent through the client!");

            // ************
            // *** TEST ***: Icmp
            oldClientSentByteCount = client.SentByteCount;
            oldClientReceivedByteCount = client.ReceivedByteCount;
            oldServerSentByteCount = serverSession.Tunnel.SentByteCount;
            oldServerReceivedByteCount = serverSession.Tunnel.ReceivedByteCount;

            TestHelper.Test_Ping();

            Assert.IsTrue(client.SentByteCount > oldClientSentByteCount + 100, "Not enough data has been sent through the client!");
            Assert.IsTrue(client.ReceivedByteCount > oldClientReceivedByteCount + 100, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.SentByteCount > oldServerSentByteCount + 100, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.ReceivedByteCount > oldServerReceivedByteCount + 100, "Not enough data has been sent through the client!");
        }

        [TestMethod]
        public void Client_must_despose_after_device_closed()
        {
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessToken(server);

            using var packetCapture = TestHelper.CreatePacketCapture();
            using var client = TestHelper.CreateClient(token, packetCapture);

            packetCapture.StopCapture();
            Assert.AreEqual(ClientState.Disposed, client.State);
        }


        [TestMethod]
        public void Client_must_despose_after_server_stopped()
        {
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessToken(server);

            // create client
            using var client = TestHelper.CreateClient(token);
            TestHelper.Test_Https();

            server.Dispose();
            try { TestHelper.Test_Https(); } catch { };
            TestHelper.WaitForClientState(client, ClientState.Disposed);
        }

        [TestMethod]
        public void Datagram_channel_after_client_reconnection()
        {
            //create a shared udp client among connection
            // make sure using same local port to test Nat properly
            using var udpClient = new UdpClient();
            using var ping = new Ping();

            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessToken(server);

            // create client
            using var client1 = TestHelper.CreateClient(token);

            // test Icmp & Udp
            TestHelper.Test_Ping(ping);
            TestHelper.Test_Udp(udpClient);

            // create client
            using var client2 = TestHelper.CreateClient(token);

            // test Icmp & Udp
            TestHelper.Test_Ping(ping);
            TestHelper.Test_Udp(udpClient);
        }


        [TestMethod]
        public void AutoReconnect()
        {
            using var httpClient = new HttpClient();

            // creae server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessToken(server);

            // ************
            // *** TEST ***: Reconnect after disconnection (1st time)
            using var clientConnect = TestHelper.CreateClientConnect(token, connectOptions: new() { MaxReconnectCount = 1, ReconnectDelay = 0 });
            Assert.AreEqual(ClientState.Connected, clientConnect.Client.State); // checkpoint
            TestHelper.Test_Https(); //let transfer something
            server.SessionManager.GetSessionById(clientConnect.Client.SessionId)?.Dispose();

            try { TestHelper.Test_Https(); } catch { }
            TestHelper.WaitForClientState(clientConnect.Client, ClientState.Connected);
            Assert.AreEqual(1, clientConnect.AttemptCount);
            TestTunnel(server, clientConnect.Client);

            // ************
            // *** TEST ***: dispose after second try (2st time)
            Assert.AreEqual(ClientState.Connected, clientConnect.Client.State); // checkpoint
            server.SessionManager.CloseSession(clientConnect.Client.SessionId);

            try { TestHelper.Test_Https(); } catch { }
            TestHelper.WaitForClientState(clientConnect.Client, ClientState.Disposed);
            Assert.AreEqual(1, clientConnect.AttemptCount);
        }

        [TestMethod]
        public void AutoReconnect_is_not_expected_for_first_attempt()
        {
            // creae server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessToken(server);

            token.TokenId = Guid.NewGuid();
            using var clientConnect = TestHelper.CreateClientConnect(token, autoConnect: false, connectOptions: new() { MaxReconnectCount = 3, ReconnectDelay = 0 });
            try
            {
                clientConnect.Connect().Wait();
                Assert.Fail("Exception expected! Client should connect with invalid token");
            }
            catch { }
            TestHelper.WaitForClientState(clientConnect.Client, ClientState.Disposed);
            Assert.AreEqual(0, clientConnect.AttemptCount, "Reconnect is not expected for first try");
        }

        [TestMethod]
        public void Reset_tcp_connection_immediately_after_vpn_connected()
        {
            // create server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessToken(server);

            using TcpClient tcpClient = new(TestHelper.TEST_HttpsUri1.Host, 443) { NoDelay = true };
            using var stream = tcpClient.GetStream();

            // create client
            using var client1 = TestHelper.CreateClient(token);

            try
            {
                stream.WriteByte(1);
                stream.ReadByte();
            }
            catch (Exception ex)
            {
                if (ex?.InnerException is not SocketException socketException || socketException.SocketErrorCode != SocketError.ConnectionReset)
                    throw;
            }
        }

        [TestMethod]
        public void Disconnect_if_session_expired()
        {
            using var fileAccessServer = new FileAccessServer(Path.Combine(TestHelper.WorkingPath, $"AccessServer_{Guid.NewGuid()}"));
            using var testAccessServer = new TestAccessServer(fileAccessServer);

            // create server
            using var server = TestHelper.CreateServer(testAccessServer);
            var token = TestHelper.CreateAccessToken(server);

            // connect
            using var client = TestHelper.CreateClient(token);
            Assert.AreEqual(ClientState.Connected, client.State);

            // restart server
            server.SessionManager.CloseSession(client.SessionId);

            // wait for disposing session in access server
            for (var i = 0; i < 6; i++)
            {
                if (!fileAccessServer.SessionManager.Sessions.TryGetValue(client.SessionId, out var session) || !session.IsAlive)
                    break;
                Thread.Sleep(200);
            }

            try { TestHelper.Test_Https(); }
            catch { }

            TestHelper.WaitForClientState(client, ClientState.Disposed, 5000);
        }

        [TestMethod]
        public void Subscribe_Maintenance_Server()
        {
            // ************
            // *** TEST ***: AccesServer is on at start
            using var fileAccessServer = new FileAccessServer(Path.Combine(TestHelper.WorkingPath, $"AccessServer_{Guid.NewGuid()}"));
            using var testAccessServer = new TestAccessServer(fileAccessServer);

            using var server = TestHelper.CreateServer(testAccessServer);

            Assert.IsFalse(server.AccessServer.IsMaintenanceMode);
            Assert.AreEqual(Environment.Version, fileAccessServer.SubscribedServerInfo?.EnvironmentVersion);
            Assert.AreEqual(Environment.MachineName, fileAccessServer.SubscribedServerInfo?.MachineName);
            Assert.IsTrue(fileAccessServer.ServerStatus?.ThreadCount > 0);
            server.Dispose();

            // ************
            // *** TEST ***: AccesServer is off at start
            testAccessServer.EmbedIoAccessServer.Stop();
            using var server2 = TestHelper.CreateServer(testAccessServer, autoStart: false);
            server2.Start().Wait();
            Assert.AreEqual(server2.State, ServerState.Subscribing);

            // ************
            // *** TEST ***: MaintenanceMode is expected
            var token = TestHelper.CreateAccessToken(fileAccessServer, server2.TcpHostEndPoint);
            using var client = TestHelper.CreateClient(token, autoConnect: false);
            try
            {
                client.Connect().Wait();
                TestHelper.WaitForClientState(client, ClientState.Disposed);
            }
            catch (Exception) { }
            Assert.AreEqual(SessionErrorCode.Maintenance, client.SessionStatus.ErrorCode);
            Assert.AreEqual(ClientState.Disposed, client.State);

            // ************
            // *** TEST ***: Connect after Maintenance is done
            testAccessServer.EmbedIoAccessServer.Start();
            using var client2 = TestHelper.CreateClient(token);
            TestHelper.WaitForClientState(client2, ClientState.Connected);
        }

        [TestMethod]
        public void Disconnect_if_UnsupportedClient()
        {
            // create server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessToken(server);
            var accessServer = server.AccessServer;

            // create client
            using var client = TestHelper.CreateClient(token, autoConnect: false, options: new ClientOptions { Version = Version.Parse("0.0.1") });

            try
            {
                var _ = client.Connect();
                TestHelper.WaitForClientState(client, ClientState.Disposed);
            }
            catch
            {
                Assert.AreEqual(SessionErrorCode.UnsupportedClient, client.SessionStatus.ErrorCode);
            }
        }
    }
}

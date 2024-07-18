using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DOL.Config;
using log4net;

namespace DOL.Network
{
    public class BaseServer
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly Encoding defaultEncoding = CodePagesEncodingProvider.Instance.GetEncoding(1252);
        public const int SEND_UDP_BUFFER_SIZE = 8192;
        private const int RECEIVE_UDP_BUFFER_SIZE = 8192;
        private const string UDP_THREAD_NAME = "UDP";
        private const int UDP_RECEIVE_BUFFER_CHUNK_SIZE = 64; // This should be increased if someday clients send UDP packets larger than this.

        protected BaseServerConfig _config;
        private Socket _listen;
        private Socket _udpSocket;
        private ConcurrentQueue<SocketAsyncEventArgs> _udpReceiveArgsPool = new();
        private SocketAsyncEventArgs _udpReceiveArgs;
        private static Thread _udpThread;

        public BaseServerConfig Configuration => _config;
        public bool IsRunning => _listen != null && _listen.Connected;

        protected BaseServer(BaseServerConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public virtual bool Start()
        {
            if (!InitializeListenSocket())
                return false;

            InitializeUdpSocket();
            ConfigureUpnp();

            if (_listen != null && _listen.IsBound)
            {
                if (!StartListen())
                    return false;
            }

            if (_udpSocket != null && _udpSocket.IsBound)
                StartUdpThread();

            return true;

            void ConfigureUpnp()
            {
                try
                {
                    UpnpNat nat = new();

                    if (!nat.Discover())
                        throw new Exception("[UPNP] Unable to access the UPnP Internet Gateway Device");

                    log.Debug("[UPNP] Current UPnP mappings:");

                    foreach (UpnpNat.PortForwarding info in nat.ListForwardedPort())
                        log.Debug($"[UPNP] {info.description} - {info.externalPort} -> {info.internalIP}:{info.internalPort}({info.protocol}) ({(info.enabled ? "enabled" : "disabled")})");

                    IPAddress localAddress = Configuration.IP;
                    nat.ForwardPort(Configuration.UDPPort, Configuration.UDPPort, ProtocolType.Udp, "DOL UDP", localAddress);
                    nat.ForwardPort(Configuration.Port, Configuration.Port, ProtocolType.Tcp, "DOL TCP", localAddress);

                    if (Configuration.DetectRegionIP)
                    {
                        try
                        {
                            Configuration.RegionIP = nat.GetExternalIP();
                            log.Debug($"[UPNP] Found the RegionIP: {Configuration.RegionIP}");
                        }
                        catch (Exception e)
                        {
                            log.Warn("[UPNP] Unable to detect the RegionIP, It is possible that no mappings exist yet", e);
                        }
                    }
                }
                catch (Exception e)
                {
                    log.Warn(e.Message, e);
                }
            }

            bool StartListen()
            {
                try
                {
                    _listen.Listen(100);
                    SocketAsyncEventArgs listenArgs = new();
                    listenArgs.Completed += OnAsyncListenCompletion;
                    _listen.AcceptAsync(listenArgs);
                    log.Info("Server is now listening to incoming connections!");
                }
                catch (Exception e)
                {
                    log.Error(e);
                    _listen?.Close();
                    return false;
                }

                return true;
            }

            void StartUdpThread()
            {
                // This is probably a bit more complicated than it should be if we consider the fact that clients only send UDP packets to notify the server that they can receive UDP packets.
                // However, it allows for simultaneous calls to `OnUdpReceive`, meaning we don't have to wait for it to end, and new data can be processed immediately.
                // Since only one buffer is used and shared, this requires some synchronization to prevent `ReceiveFromAsync` from overwriting data that isn't processed yet.
                // For this reason, the buffer is split in chunks of `UDP_RECEIVE_BUFFER_CHUNK_SIZE` bytes. This assumes no packet can be larger than this.

                _udpThread = new Thread(new ThreadStart(RunUdpThread))
                {
                    Name = UDP_THREAD_NAME,
                    IsBackground = true
                };
                _udpThread.Start();

                async void RunUdpThread()
                {
                    ConcurrentQueue<int> availablePositions = [];

                    for (int i = 0; i < RECEIVE_UDP_BUFFER_SIZE; i += UDP_RECEIVE_BUFFER_CHUNK_SIZE)
                        availablePositions.Enqueue(i);

                    EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] buffer = new byte[RECEIVE_UDP_BUFFER_SIZE];
                    int position;

                    do
                    {
                        try
                        {
                            // Spinning isn't great, but clients shouldn't send enough packets, or the buffer size be small enough, or tasks take long enough for this to happen regularly.
                            while (!availablePositions.TryDequeue(out position))
                                Thread.Yield();

                            int offset = position;
                            SocketReceiveFromResult result = await _udpSocket.ReceiveFromAsync(new ArraySegment<byte>(buffer, offset, UDP_RECEIVE_BUFFER_CHUNK_SIZE), endPoint);

                            _ = Task.Run(() =>
                            {
                                OnUdpReceive(buffer, offset, result.ReceivedBytes, result.RemoteEndPoint);
                                availablePositions.Enqueue(offset);
                            });
                        }
                        catch (Exception e)
                        {
                            if (log.IsErrorEnabled)
                                log.Error(e);

                            return;
                        }
                    } while (true);
                }
            }

            void OnAsyncListenCompletion(object sender, SocketAsyncEventArgs listenArgs)
            {
                if (_listen == null || listenArgs.SocketError is SocketError.ConnectionReset)
                    return;

                BaseClient baseClient = null;
                Socket socket = listenArgs.AcceptSocket;

                try
                {
                    baseClient = GetNewClient(socket);
                    baseClient.Receive();
                    baseClient.OnConnect();
                }
                catch (Exception e)
                {
                    log.Error(e);

                    if (baseClient != null)
                        Disconnect(baseClient);

                    if (socket != null)
                    {
                        try
                        {
                            socket.Close();
                        }
                        catch { }
                    }
                }
                finally
                {
                    listenArgs.AcceptSocket = null;
                    _listen.AcceptAsync(listenArgs);
                }
            }
        }

        protected virtual BaseClient GetNewClient(Socket socket)
        {
            return new BaseClient(this, socket);
        }

        protected virtual bool InitializeListenSocket()
        {
            try
            {
                _listen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listen.Bind(new IPEndPoint(_config.IP, _config.Port));
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error(e);

                return false;
            }

            return true;
        }

        protected virtual bool InitializeUdpSocket()
        {
            try
            {
                _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _udpSocket.Bind(new IPEndPoint(_config.UDPIP, _config.UDPPort));
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error(e);

                return false;
            }

            return true;
        }

        public bool SendUdp(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            return _udpSocket.SendToAsync(socketAsyncEventArgs);
        }

        public virtual void Stop()
        {
            /*if(Configuration.EnableUPNP)
            {
                try
                {
                    if(Log.IsDebugEnabled)
                        Log.Debug("Removing UPnP Mappings");
                    UPnPNat nat = new UPnPNat();
                    PortMappingInfo pmiUDP = new PortMappingInfo("UDP", Configuration.UDPPort);
                    PortMappingInfo pmiTCP = new PortMappingInfo("TCP", Configuration.Port);
                    nat.RemovePortMapping(pmiUDP);
                    nat.RemovePortMapping(pmiTCP);
                }
                catch(Exception ex)
                {
                    if(Log.IsDebugEnabled)
                        Log.Debug("Failed to remove UPnP Mappings", ex);
                }
            }*/

            try
            {
                if (_listen != null)
                {
                    _listen.Close();
                    _listen = null;

                    log.Info("Server is no longer listening for incoming connections");
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error(e);
            }

            if (_udpThread != null)
            {
                _udpThread.Interrupt();
                _udpThread.Join();
                _udpThread = null;
            }

            try
            {
                if (_udpSocket != null)
                {
                    _udpSocket.Close();
                    _udpSocket = null;
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error(e);
            }

            if (log.IsInfoEnabled)
                log.Info("Server stopped");
        }

        public virtual bool Disconnect(BaseClient baseClient)
        {
            try
            {
                baseClient.OnDisconnect();
                baseClient.CloseConnections();
            }
            catch (Exception e)
            {
                log.Error("Exception", e);
                return false;
            }

            return true;
        }

        protected virtual void OnUdpReceive(byte[] buffer, int offset, int size, EndPoint endPoint) { }
    }
}

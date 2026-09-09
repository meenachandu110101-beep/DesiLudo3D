using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>$0 local-network transport for the validated net protocol:
    /// UDP discovery beacons (host announces room code) + TCP session lines
    /// (host-authoritative commands/events). Pure .NET sockets — IL2CPP/ARM64
    /// safe, no packages, no relay service, no accounts (D6: P2P LAN).
    /// Lines are marshalled to the main thread via queues (Unity API safety).</summary>
    public class LanTransport : MonoBehaviour
    {
        public const int TcpPort = 4760;
        public const int BeaconPort = 4761;

        // --------------------------------------------------------- discovery

        UdpClient _beaconOut;
        Thread _beaconThread;
        volatile bool _beaconRunning;
        string _beaconPayload;

        UdpClient _listener;
        Thread _listenThread;
        volatile bool _listenRunning;

        public class RoomInfo
        {
            public string Code;
            public string Name;
            public int Current;
            public int Max;
            public string Ip;
            public float LastSeen;
        }

        readonly Dictionary<string, RoomInfo> _rooms = new Dictionary<string, RoomInfo>();
        public IReadOnlyCollection<RoomInfo> Rooms
        {
            get
            {
                PruneRooms();
                return _rooms.Values;
            }
        }

        void PruneRooms()
        {
            float now = Time.unscaledTime;
            var dead = new List<string>();
            foreach (var kv in _rooms)
                if (now - kv.Value.LastSeen > 6f) dead.Add(kv.Key);
            foreach (string code in dead) _rooms.Remove(code);
        }

        /// <summary>Host: start announcing the room on the local network.</summary>
        public void StartBeacon(string roomCode, string hostName, int current, int max)
        {
            StopBeacon();
            _beaconPayload = "DESIROOM " + roomCode + " " + hostName + " "
                           + current + "/" + max;
            _beaconRunning = true;
            _beaconThread = new Thread(BeaconLoop) { IsBackground = true };
            _beaconThread.Start();
        }

        public void StopBeacon()
        {
            _beaconRunning = false;
            _beaconOut?.Close();
            _beaconOut = null;
        }

        void BeaconLoop()
        {
            try
            {
                _beaconOut = new UdpClient();
                _beaconOut.EnableBroadcast = true;
                var endpoint = new IPEndPoint(IPAddress.Broadcast, BeaconPort);
                while (_beaconRunning)
                {
                    byte[] data = Encoding.UTF8.GetBytes(_beaconPayload);
                    _beaconOut.Send(data, data.Length, endpoint);
                    Thread.Sleep(1200);
                }
            }
            catch (Exception) { /* beacon stopped or network gone */ }
        }

        /// <summary>Client: listen for room beacons (call StopDiscovery when
        /// done). Results appear in `Rooms`.</summary>
        public void StartDiscovery()
        {
            StopDiscovery();
            _listenRunning = true;
            _listenThread = new Thread(DiscoveryLoop) { IsBackground = true };
            _listenThread.Start();
        }

        public void StopDiscovery()
        {
            _listenRunning = false;
            _listener?.Close();
            _listener = null;
        }

        void DiscoveryLoop()
        {
            try
            {
                _listener = new UdpClient(BeaconPort);
                _listener.EnableBroadcast = true;
                var any = new IPEndPoint(IPAddress.Any, 0);
                while (_listenRunning)
                {
                    byte[] data = _listener.Receive(ref any);
                    string line = Encoding.UTF8.GetString(data);
                    if (!line.StartsWith("DESIROOM ")) continue;
                    string[] p = line.Split(' ');
                    if (p.Length < 4) continue;
                    string[] counts = p[3].Split('/');
                    if (counts.Length != 2) continue;
                    var info = new RoomInfo
                    {
                        Code = p[1],
                        Name = p[2],
                        Current = int.Parse(counts[0]),
                        Max = int.Parse(counts[1]),
                        Ip = any.Address.ToString(),
                        LastSeen = Time.unscaledTime,
                    };
                    lock (_rooms) _rooms[info.Code] = info;
                }
            }
            catch (Exception) { /* listener closed */ }
        }

        // ------------------------------------------------------- tcp session

        public class HostSide
        {
            readonly LanTransport _owner;
            readonly TcpListener _listener;
            readonly List<TcpClient> _clients = new List<TcpClient>();
            readonly Dictionary<TcpClient, int> _ids = new Dictionary<TcpClient, int>();
            readonly ConcurrentQueue<(int Id, string Line)> _inbound =
                new ConcurrentQueue<(int, string)>();
            int _nextId = 1;
            volatile bool _running = true;
            object _writeLock = new object();

            public Action<int, string> OnLine = (_, __) => { };

            public HostSide(LanTransport owner, int maxClients)
            {
                _owner = owner;
                _listener = new TcpListener(IPAddress.Any, TcpPort);
                _listener.Start(maxClients);
                var t = new Thread(AcceptLoop) { IsBackground = true };
                t.Start();
            }

            void AcceptLoop()
            {
                try
                {
                    while (_running)
                    {
                        TcpClient client = _listener.AcceptTcpClient();
                        client.NoDelay = true;
                        int id;
                        lock (_clients)
                        {
                            id = _nextId++;
                            _clients.Add(client);
                            _ids[client] = id;
                        }
                        var rt = new Thread(() => ReadLoop(client)) { IsBackground = true };
                        rt.Start();
                    }
                }
                catch (Exception) { /* listener stopped */ }
            }

            void ReadLoop(TcpClient client)
            {
                try
                {
                    var stream = client.GetStream();
                    var buf = new byte[4096];
                    var sb = new StringBuilder();
                    while (_running)
                    {
                        int n = stream.Read(buf, 0, buf.Length);
                        if (n == 0) break;
                        sb.Append(Encoding.UTF8.GetString(buf, 0, n));
                        string text = sb.ToString();
                        int nl;
                        while ((nl = text.IndexOf('\n')) >= 0)
                        {
                            string line = text.Substring(0, nl).Trim('\r');
                            text = text.Substring(nl + 1);
                            if (line.Length > 0)
                                _inbound.Enqueue((_ids[client], line));
                        }
                        sb = new StringBuilder(text);
                    }
                }
                catch (Exception) { /* connection lost */ }
                lock (_clients)
                {
                    _clients.Remove(client);
                    _ids.Remove(client);
                }
            }

            public void Pump(Action<int, string> deliver)
            {
                while (_inbound.TryDequeue(out var msg))
                    deliver(msg.Id, msg.Line);
            }

            public void SendTo(int guestId, string line)
            {
                byte[] data = Encoding.UTF8.GetBytes(line + "\n");
                lock (_clients)
                {
                    foreach (var kv in _ids)
                    {
                        if (kv.Value != guestId) continue;
                        try
                        {
                            lock (_writeLock)
                                kv.Key.GetStream().Write(data, 0, data.Length);
                        }
                        catch (Exception) { /* guest gone; resync on return */ }
                    }
                }
            }

            public void Broadcast(string line)
            {
                byte[] data = Encoding.UTF8.GetBytes(line + "\n");
                lock (_clients)
                {
                    foreach (TcpClient c in _clients)
                    {
                        try
                        {
                            lock (_writeLock)
                                c.GetStream().Write(data, 0, data.Length);
                        }
                        catch (Exception) { }
                    }
                }
            }

            public int GuestCount
            {
                get { lock (_clients) return _clients.Count; }
            }

            public void Close()
            {
                _running = false;
                try { _listener.Stop(); } catch (Exception) { }
                lock (_clients)
                {
                    foreach (TcpClient c in _clients)
                        try { c.Close(); } catch (Exception) { }
                    _clients.Clear();
                    _ids.Clear();
                }
            }
        }

        public class GuestSide
        {
            readonly ConcurrentQueue<string> _inbound = new ConcurrentQueue<string>();
            TcpClient _client;
            volatile bool _running;
            public bool IsConnected => _client?.Connected == true && _running;
            public string HostIp { get; private set; }

            /// <summary>Connect to a host. Returns false when unreachable.</summary>
            public bool Connect(string hostIp)
            {
                Disconnect();
                try
                {
                    HostIp = hostIp;
                    _client = new TcpClient();
                    var result = _client.BeginConnect(hostIp, TcpPort, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(4000))
                    {
                        _client.Close();
                        _client = null;
                        return false;
                    }
                    _client.EndConnect(result);
                    _client.NoDelay = true;
                    _running = true;
                    var t = new Thread(ReadLoop) { IsBackground = true };
                    t.Start();
                    return true;
                }
                catch (Exception)
                {
                    _client = null;
                    return false;
                }
            }

            void ReadLoop()
            {
                try
                {
                    var stream = _client.GetStream();
                    var buf = new byte[4096];
                    var sb = new StringBuilder();
                    while (_running)
                    {
                        int n = stream.Read(buf, 0, buf.Length);
                        if (n == 0) break;
                        sb.Append(Encoding.UTF8.GetString(buf, 0, n));
                        string text = sb.ToString();
                        int nl;
                        while ((nl = text.IndexOf('\n')) >= 0)
                        {
                            string line = text.Substring(0, nl).Trim('\r');
                            text = text.Substring(nl + 1);
                            if (line.Length > 0) _inbound.Enqueue(line);
                        }
                        sb = new StringBuilder(text);
                    }
                }
                catch (Exception) { /* dropped: caller reconnects */ }
                _running = false;
            }

            public void Pump(Action<string> deliver)
            {
                while (_inbound.TryDequeue(out var line)) deliver(line);
            }

            public void Send(string line)
            {
                if (!IsConnected) return;
                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(line + "\n");
                    _client.GetStream().Write(data, 0, data.Length);
                }
                catch (Exception) { _running = false; }
            }

            public void Disconnect()
            {
                _running = false;
                try { _client?.Close(); } catch (Exception) { }
                _client = null;
            }
        }

        void OnDestroy()
        {
            StopBeacon();
            StopDiscovery();
        }
    }
}

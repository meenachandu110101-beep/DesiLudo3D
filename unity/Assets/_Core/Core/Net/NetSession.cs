using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using DesiLudo3D.Rules;

namespace DesiLudo3D.Core
{
    /// <summary>Online play protocol — C# mirror of the VALIDATED Python
    /// reference (ludo/netproto.py, tests/test_netproto.py: full matches,
    /// tamper/drop/disconnect fault injection, unfair-die detection,
    /// reconnect catch-up, byte-identical logs). Host-authoritative,
    /// event-sourced, commit–reveal fair dice, HMAC-signed messages.</summary>
    public static class NetProtocol
    {
        public const int Version = 1;

        public static readonly string RoomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>splitmix64 — identical stream to ludo.netproto.Rng and
        /// Core/Comedy/ComedyDirector.Rng (golden-anchored in tests).</summary>
        public struct SplitMix64
        {
            ulong _s;
            public SplitMix64(ulong seed) { _s = seed; }

            public ulong Next()
            {
                _s += 0x9E3779B97F4A7C15UL;
                ulong z = _s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }

            public int NextInt(int n) => (int)(Next() % (ulong)n);
        }

        public static string MakeRoomCode(SplitMix64 rng, int len = 6)
        {
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
                sb.Append(RoomAlphabet[rng.NextInt(RoomAlphabet.Length)]);
            return sb.ToString();
        }

        /// <summary>SHA-256(concat seeds in seat order), first 8 bytes
        /// big-endian — the die-stream seed (IL-012 extended, mirrored).</summary>
        public static ulong CombineStreamSeed(byte[][] seeds)
        {
            if (seeds == null || seeds.Length < 2)
                throw new ArgumentException("commit-reveal needs at least two seeds");
            using (var sha = SHA256.Create())
            {
                foreach (byte[] s in seeds) sha.TransformBlock(s, 0, s.Length, null, 0);
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                byte[] d = sha.Hash;
                ulong v = 0;
                for (int i = 0; i < 8; i++) v = (v << 8) | d[i];
                return v;
            }
        }

        public static byte[] DeriveSessionKey(string roomCode, string nonceHex)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(
                    "desiludo-session:" + roomCode + ":" + nonceHex));
        }

        public static string Sign(byte[] key, string payload)
        {
            using (var h = new HMACSHA256(key))
                return Hex(h.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        public static bool VerifySig(byte[] key, string payload, string sig)
        {
            if (string.IsNullOrEmpty(sig)) return false;
            return string.Equals(Sign(key, payload), sig, StringComparison.OrdinalIgnoreCase);
        }

        public static string Hex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static byte[] Unhex(string hex)
        {
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return b;
        }
    }

    /// <summary>Authoritative session host: wraps the VALIDATED MatchRunner
    /// (the same engine used offline — online and offline share one rules
    /// authority). Pure C# over send/broadcast delegates: LAN (M5) and any
    /// future internet transport (M6+/EOS behind INetTransport) feed lines
    /// here. Wire semantics mirror ludo.netproto.NetHost exactly.</summary>
    public sealed class NetHostSession
    {
        readonly string _roomCode;
        readonly string _hostName;
        readonly List<Seat> _seats = new List<Seat>();
        readonly Dictionary<int, string> _guestSeat = new Dictionary<int, string>();   // guestId -> seat name
        readonly Dictionary<Seat, string> _seatOwner = new Dictionary<Seat, string>();
        readonly Dictionary<Seat, string> _commitments = new Dictionary<Seat, string>();
        readonly Dictionary<Seat, byte[]> _reveals = new Dictionary<Seat, byte[]>();

        public MatchRunner Runner { get; private set; }
        public bool MatchStarted { get; private set; }
        public int Seq { get; private set; }
        public string RoomCode => _roomCode;
        public Seat[] Seats => _seats.ToArray();

        /// <summary>seat -> "__host__" | "guest" | "bot" (lobby/start).</summary>
        public IReadOnlyDictionary<Seat, string> SeatOwners => _seatOwner;

        /// <summary>True when the seat is host/guest/bot-claimed.</summary>
        public bool IsSeatClaimed(Seat seat) => _seatOwner.ContainsKey(seat);

        public int GuestCount => _guestSeat.Count;

        /// <summary>Fired when the fair stream begins (all commits+reveals
        /// in) — the host UI builds the match scene here.</summary>
        public event Action MatchStartedEvent;

        /// <summary>(seq, command line, state hash) — the resync source.</summary>
        public readonly List<(int Seq, string Line, string StateHash)> CommandLog =
            new List<(int, string, string)>();

        byte[] _sessionKey;
        SplitMix64 _stream;
        public ulong StreamSeed { get; private set; }

        public Action<int, string> Send;          // guestId, line
        public Action<string> Broadcast;          // line -> all guests
        /// <summary>Raised on every applied command (host-side animation).</summary>
        public event Action<Command, IReadOnlyList<GameEvent>> CommandAppliedLocal;

        public NetHostSession(string roomCode, string hostName, int players, bool blockades)
        {
            _roomCode = roomCode;
            _hostName = hostName;
            foreach (Seat s in Seats.ForPlayerCount(players)) _seats.Add(s);
            Blockades = blockades;
            Send = (_, __) => { };
            Broadcast = _ => { };
            _sessionNonce = NetProtocol.Hex(Guid.NewGuid().ToByteArray()).Substring(0, 16);
            _sessionKey = NetProtocol.DeriveSessionKey(roomCode, _sessionNonce);
        }

        public bool Blockades { get; }

        readonly string _sessionNonce;

        public void HostCommit()
        {
            Seat seat = HostSeat();
            _seatOwner[seat] = "__host__";
            byte[] seed = CommitRevealDice.CreateSeed();
            RegisterCommit(seat, CommitRevealDice.Commitment(seed));
            RegisterReveal(seat, seed);
        }

        /// <summary>Commit+reveal on behalf of a bot-filling seat (host
        /// vouches for bot seeds — guests' seeds still protect fairness;
        /// bots are not adversarial). Call before guests reveal.</summary>
        public void BotCommit(Seat seat)
        {
            _seatOwner[seat] = "bot";
            byte[] seed = CommitRevealDice.CreateSeed();
            RegisterCommit(seat, CommitRevealDice.Commitment(seed));
            RegisterReveal(seat, seed);
        }

        Seat HostSeat()
        {
            foreach (Seat s in _seats)
                if (_seatOwner.TryGetValue(s, out var o) && o == "__host__") return s;
            foreach (Seat s in _seats)
                if (!_seatOwner.ContainsKey(s)) return s;
            return _seats[0];
        }

        // ------------------------------------------------------------ inbound

        public void OnLine(int guestId, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            string[] p = line.Split(' ');
            switch (p[0])
            {
                case "hello":
                {
                    if (p.Length < 3) { Send(guestId, "bye malformed"); return; }
                    if (p[1] != _roomCode) { Send(guestId, "bye bad-room-code"); return; }
                    if (MatchStarted || _guestSeat.Count >= _seats.Count - 1)
                    { Send(guestId, "bye room-full"); return; }
                    Seat seat = FreeSeat();
                    _guestSeat[guestId] = Seats.Name(seat);
                    _seatOwner[seat] = "guest";
                    Send(guestId, "welcome " + (int)seat + " " + _sessionNonce + " "
                                + NetProtocol.Hex(_sessionKey));
                    Broadcast("lobby " + (_guestSeat.Count + 1) + "/" + _seats.Count);
                    return;
                }
                case "commit":
                {
                    if (!_guestSeat.TryGetValue(guestId, out var sn)) return;
                    RegisterCommit(Seats.Parse(sn), p[1]);
                    return;
                }
                case "reveal":
                {
                    if (!_guestSeat.TryGetValue(guestId, out var sn)) return;
                    RegisterReveal(Seats.Parse(sn), NetProtocol.Unhex(p[1]));
                    return;
                }
                case "cmd":
                {
                    if (!_guestSeat.TryGetValue(guestId, out var sn)) { Send(guestId, "err not-in-room"); return; }
                    if (!MatchStarted) { Send(guestId, "err not-started"); return; }
                    OnGuestCmd(guestId, Seats.Parse(sn), p);
                    return;
                }
                case "resume":
                {
                    if (!_guestSeat.ContainsKey(guestId)) { Send(guestId, "bye not-in-room"); return; }
                    int last = p.Length > 1 ? int.Parse(p[1]) : 0;
                    ResendFrom(guestId, last);
                    return;
                }
                case "ack":
                    return;   // seq tracking reserved (transport-agnostic acks)
                case "bye":
                    DropGuest(guestId);
                    return;
            }
        }

        Seat FreeSeat()
        {
            foreach (Seat s in _seats)
                if (!_seatOwner.ContainsKey(s)) return s;
            return _seats[0];
        }

        public void DropGuest(int guestId)
        {
            if (!_guestSeat.TryGetValue(guestId, out var sn)) return;
            _guestSeat.Remove(guestId);
            Seat seat = Seats.Parse(sn);
            _seatOwner.Remove(seat);
            if (!MatchStarted)
            {
                _commitments.Remove(seat);
                _reveals.Remove(seat);
            }
            Broadcast("lobby " + (_guestSeat.Count + 1) + "/" + _seats.Count);
        }

        // ------------------------------------------------------ commit-reveal

        void RegisterCommit(Seat seat, string commit)
        {
            if (MatchStarted || _commitments.ContainsKey(seat)) return;
            _commitments[seat] = commit;
            if (_commitments.Count == _seats.Count) StartMatch();
        }

        void RegisterReveal(Seat seat, byte[] seed)
        {
            if (MatchStarted || !_commitments.TryGetValue(seat, out var commit)) return;
            if (_reveals.ContainsKey(seat)) return;
            if (CommitRevealDice.Commitment(seed) != commit)
            {
                int gid = GuestOfSeat(seat);
                if (gid >= 0)
                {
                    Send(gid, "bye reveal-mismatch " + Seats.Name(seat));
                    DropGuest(gid);
                }
                _reveals.Remove(seat);
                _commitments.Remove(seat);
                return;
            }
            _reveals[seat] = seed;
            if (_reveals.Count == _seats.Count) BeginStream();
        }

        void StartMatch()
        {
            var sb = new StringBuilder("start ")
                .Append(_seats.Count).Append(' ').Append(Blockades ? 1 : 0);
            foreach (Seat s in _seats) sb.Append(' ').Append(_commitments[s]);
            BroadcastSigned(sb.ToString());
        }

        void BeginStream()
        {
            var seeds = new byte[_seats.Count][];
            for (int i = 0; i < _seats.Count; i++) seeds[i] = _reveals[_seats[i]];
            StreamSeed = NetProtocol.CombineStreamSeed(seeds);
            _stream = new SplitMix64(StreamSeed);
            Runner = new MatchRunner(BuildConfig(), new FixedSequenceDiceProvider(1));
            Runner.CommandApplied += OnRunnerCommandApplied;
            MatchStarted = true;
            var sb = new StringBuilder("seeds");
            foreach (byte[] s in seeds) sb.Append(' ').Append(NetProtocol.Hex(s));
            BroadcastSigned(sb.ToString());
            MatchStartedEvent?.Invoke();
        }

        MatchConfig BuildConfig() => new MatchConfig(
            _seats.ToArray(), blockades: Blockades,
            threeSixes: true, captureExtra: true, placings: true);

        // ------------------------------------------------------------- match

        void OnRunnerCommandApplied(Command command, IReadOnlyList<GameEvent> events, string error)
        {
            if (error != null)
            {
                Seat actor = SeatOf(command);
                int gid = actor >= 0 ? GuestOfSeat(actor) : -1;
                if (gid >= 0) Send(gid, "err illegal " + error);
                return;
            }
            Seq++;
            CommandLog.Add((Seq, command.Line(), Runner.State.StateHash()));
            BroadcastSigned("cmd " + Seq + " " + command.Line());
            BroadcastSigned("state " + Seq + " " + Runner.State.StateHash());
            CommandAppliedLocal?.Invoke(command, events);
        }

        static Seat SeatOf(Command command)
        {
            switch (command)
            {
                case RollDiceCommand r: return r.Seat;
                case MoveTokenCommand m: return m.Seat;
                case LeaveMatchCommand l: return l.Seat;
                default: return (Seat)(-1);
            }
        }

        int GuestOfSeat(Seat seat)
        {
            string name = Seats.Name(seat);
            foreach (var kv in _guestSeat)
                if (kv.Value == name) return kv.Key;
            return -1;
        }

        void OnGuestCmd(int guestId, Seat seat, string[] p)
        {
            if (Runner == null) return;
            if (Runner.CurrentSeat != seat) { Send(guestId, "err not-your-turn"); return; }
            if (p[1] == "roll")
            {
                if (Runner.IsAwaitingRoll) Runner.TryRollValue(_stream.NextInt(6) + 1);
            }
            else if (p[1] == "move")
            {
                if (Runner.IsAwaitingMove) Runner.TryMoveToken(int.Parse(p[2]));
            }
            else if (p[1] == "leave")
            {
                Runner.TryLeave(seat);
            }
            else Send(guestId, "err unknown-cmd");
        }

        /// <summary>Seat-parameterized input — used by the host device (its
        /// own seat) and by host-side bots filling vacant seats. The die is
        /// ALWAYS drawn from the fair commit-reveal stream.</summary>
        public void RollFor(Seat seat)
        {
            if (Runner != null && Runner.IsAwaitingRoll && Runner.CurrentSeat == seat)
                Runner.TryRollValue(_stream.NextInt(6) + 1);
        }

        public void MoveFor(Seat seat, int tokenIndex)
        {
            if (Runner != null && Runner.IsAwaitingMove && Runner.CurrentSeat == seat)
                Runner.TryMoveToken(tokenIndex);
        }

        /// <summary>Host-seat input (host device UI calls these).</summary>
        public void HostRoll() => RollFor(HostSeat());

        public void HostMove(int tokenIndex) => MoveFor(HostSeat(), tokenIndex);

        // ------------------------------------------------------------ resync

        public void ResendFrom(int guestId, int lastSeq)
        {
            foreach (var (seq, line, hash) in CommandLog)
            {
                if (seq <= lastSeq) continue;
                SendSigned(guestId, "cmd " + seq + " " + line);
                SendSigned(guestId, "state " + seq + " " + hash);
            }
            Send(guestId, "resynced " + Seq);
        }

        // ------------------------------------------------------------ signing

        void BroadcastSigned(string payload)
        {
            Broadcast(payload + " " + NetProtocol.Sign(_sessionKey, payload));
        }

        void SendSigned(int guestId, string payload)
        {
            Send(guestId, payload + " " + NetProtocol.Sign(_sessionKey, payload));
        }
    }

    /// <summary>Guest session: shadow-replays every verified command through
    /// the local engine, verifies HMAC signatures, sequence continuity, the
    /// fair die stream and host state hashes (defense in depth). Mirrors
    /// ludo.netproto.NetGuest.</summary>
    public sealed class NetGuestSession
    {
        GameState _state;
        SplitMix64 _stream;
        byte[] _seed;
        byte[] _sessionKey;

        public int SeatIndex = -1;
        public MatchConfig Config;
        public string[] Commitments = Array.Empty<string>();
        public List<byte[]> RevealedSeeds = new List<byte[]>();
        public int LastSeq;
        public int VerifiedHashes;
        public int SigFailures;
        public int SeqGaps;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> HashMismatches = new List<string>();
        public readonly List<(int Seq, string Line)> CommandLog = new List<(int, string)>();

        public Action<string> Send = _ => { };
        /// <summary>Raised when a command is applied to the shadow state —
        /// the guest UI animates these events.</summary>
        public event Action<Command, IReadOnlyList<GameEvent>> CommandAppliedShadow;

        /// <summary>Fired when the host's `start` message lands (the board
        /// can be built — the config is now known).</summary>
        public event Action ConfigReceived;

        /// <summary>Fired when the seeds land and the shadow state begins —
        /// the guest scene starts playing here.</summary>
        public event Action ShadowStarted;

        public GameState State => _state;
        public bool MyTurn => _state != null && SeatIndex >= 0
            && _state.CurrentSeat == (Seat)SeatIndex
            && _state.Phase != TurnPhase.MatchOver;

        public void Join(string roomCode, string name) =>
            Send("hello " + roomCode + " " + name);

        public void SendCommit(byte[] seed)
        {
            _seed = seed;
            Send("commit " + CommitRevealDice.Commitment(seed));
        }

        public void SendReveal() => Send("reveal " + NetProtocol.Hex(_seed));

        public void RequestRoll() => Send("cmd roll");

        public void RequestMove(int tokenIndex) => Send("cmd move " + tokenIndex);

        public void Resume() => Send("resume " + LastSeq);

        // ------------------------------------------------------------ inbound

        public void OnLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            string[] p = line.Split(' ');
            switch (p[0])
            {
                case "welcome":
                    SeatIndex = int.Parse(p[1]);
                    _sessionKey = NetProtocol.Unhex(p[3]);
                    return;

                case "start":
                {
                    if (!CheckSig(p)) return;
                    string[] body = p[..^1];
                    int players = int.Parse(body[1]);
                    bool blockades = body[2] == "1";
                    Config = new MatchConfig(Seats.ForPlayerCount(players),
                        blockades: blockades, threeSixes: true,
                        captureExtra: true, placings: true);
                    Commitments = body[3..(3 + players)];
                    ConfigReceived?.Invoke();
                    return;
                }
                case "seeds":
                {
                    if (!CheckSig(p)) return;
                    RevealedSeeds.Clear();
                    for (int i = 1; i < p.Length - 1; i++)
                        RevealedSeeds.Add(NetProtocol.Unhex(p[i]));
                    if (RevealedSeeds.Count >= 2)
                    {
                        var seeds = RevealedSeeds.ToArray();
                        if (CommitRevealDice.Verify(seeds, Commitments))
                        {
                            _stream = new SplitMix64(NetProtocol.CombineStreamSeed(seeds));
                            _state = RulesEngine.NewMatch(Config);
                            ShadowStarted?.Invoke();
                        }
                        else
                        {
                            Errors.Add("commit-reveal-verification-failed");
                        }
                    }
                    return;
                }
                case "cmd":
                {
                    if (!CheckSig(p)) return;
                    OnCmd(int.Parse(p[1]), JoinSpan(p, 2, p.Length - 2));
                    return;
                }
                case "state":
                {
                    if (!CheckSig(p)) return;
                    int seq = int.Parse(p[1]);
                    string hash = p[2];
                    if (seq > LastSeq)
                    {
                        SeqGaps++;       // livelock guard (mirror of Python)
                        Resume();
                    }
                    else if (seq == LastSeq && _state != null)
                    {
                        if (_state.StateHash() != hash) HashMismatches.Add("seq " + seq);
                        else VerifiedHashes++;
                    }
                    return;
                }
                case "err":
                    Errors.Add(JoinSpan(p, 1, p.Length));
                    return;
                case "bye":
                    Errors.Add("bye " + JoinSpan(p, 1, p.Length));
                    return;
            }
        }

        static string JoinSpan(string[] p, int from, int toExclusive)
        {
            var sb = new StringBuilder();
            for (int i = from; i < toExclusive; i++)
            {
                if (i > from) sb.Append(' ');
                sb.Append(p[i]);
            }
            return sb.ToString();
        }

        bool CheckSig(string[] p)
        {
            if (_sessionKey == null) { SigFailures++; return false; }
            string payload = JoinSpan(p, 0, p.Length - 1);
            if (!NetProtocol.VerifySig(_sessionKey, payload, p[p.Length - 1]))
            {
                SigFailures++;
                return false;
            }
            return true;
        }

        void OnCmd(int seq, string cmdLine)
        {
            if (seq != LastSeq + 1)
            {
                SeqGaps++;
                Resume();
                return;
            }
            Command command;
            try { command = CommandParser.Parse(cmdLine); }
            catch (FormatException) { Errors.Add("bad-cmd " + cmdLine); return; }

            ApplyResult result = RulesEngine.Apply(_state, command);
            if (!result.Ok)
            {
                Errors.Add("shadow-replay-failed " + cmdLine);
                return;
            }
            _state = result.NewState;
            LastSeq = seq;
            CommandLog.Add((seq, cmdLine));

            if (command is RollDiceCommand roll)
            {
                int expected = _stream.NextInt(6) + 1;
                if (roll.Die != expected)
                    Errors.Add("unfair-die seq " + seq + ": got " + roll.Die
                               + ", stream says " + expected);
            }
            Send("ack " + seq);
            CommandAppliedShadow?.Invoke(command, result.Events);
        }
    }
}

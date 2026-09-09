using System;
using System.Collections.Generic;
using DesiLudo3D.Rules;

namespace DesiLudo3D.Core
{
    /// <summary>Turn-flow orchestration over the pure rules engine.
    /// Pure C# (no UnityEngine) so it is unit-testable headless and reusable
    /// verbatim on the online host (spec §9.1: host runs the rules engine).
    /// All state changes flow through Apply; every applied command's events
    /// are exposed for presentation, logging and the event-sourced match log.</summary>
    public sealed class MatchRunner
    {
        readonly IDiceProvider _dice;
        GameState _state;
        readonly List<Command> _commandLog = new List<Command>();
        readonly List<GameEvent> _eventLog = new List<GameEvent>();

        /// <summary>Raised once per applied command with its full event batch
        /// (empty batch + Error != null when a command was rejected).</summary>
        public event Action<Command, IReadOnlyList<GameEvent>, string> CommandApplied;

        public MatchRunner(MatchConfig config, IDiceProvider dice)
        {
            _dice = dice ?? throw new ArgumentNullException(nameof(dice));
            _state = RulesEngine.NewMatch(config);
        }

        public GameState State => _state;
        public MatchConfig Config => _state.Config;
        public Seat CurrentSeat => _state.CurrentSeat;
        public bool IsAwaitingRoll => _state.Phase == TurnPhase.AwaitRoll;
        public bool IsAwaitingMove => _state.Phase == TurnPhase.AwaitMove;
        public bool IsMatchOver => _state.Phase == TurnPhase.MatchOver;
        public IReadOnlyList<Command> CommandLog => _commandLog;
        public IReadOnlyList<GameEvent> EventLog => _eventLog;

        public IReadOnlyList<LegalMove> PendingMoves =>
            IsAwaitingMove
                ? RulesEngine.GetLegalMoves(_state, _state.CurrentSeat, _state.LastDie)
                : new LegalMove[0];

        // ------------------------------------------------------------ actions

        /// <summary>Roll using the dice provider (the normal game path).</summary>
        public bool TryRollDie()
        {
            return TryRollValue(_dice.RollDie());
        }

        /// <summary>Roll an explicit value (deterministic tests, replays,
        /// online guests echoing host-authoritative dice events).</summary>
        public bool TryRollValue(int die)
        {
            return Apply(new RollDiceCommand(_state.CurrentSeat, die));
        }

        public bool TryMoveToken(int tokenIndex)
        {
            return Apply(new MoveTokenCommand(_state.CurrentSeat, tokenIndex));
        }

        public bool TryLeave(Seat seat)
        {
            return Apply(new LeaveMatchCommand(seat));
        }

        bool Apply(Command command)
        {
            ApplyResult result = RulesEngine.Apply(_state, command);
            if (!result.Ok)
            {
                CommandApplied?.Invoke(command, result.Events, result.Error);
                return false;
            }
            _state = result.NewState;
            _commandLog.Add(command);
            foreach (GameEvent e in result.Events) _eventLog.Add(e);
            CommandApplied?.Invoke(command, result.Events, null);
            return true;
        }

        // ------------------------------------------------------------ persistence

        /// <summary>Serialize the live match (state + logs) for save/resume.</summary>
        public string Save()
        {
            return MatchCodec.SaveState(_state)
                 + "\n" + MatchCodec.CommandLogHeader + "\n"
                 + string.Join("\n", ConvertAll(_commandLog))
                 + "\n" + MatchCodec.EventLogHeader + "\n"
                 + string.Join("\n", ConvertAll(_eventLog));
        }

        /// <summary>Restore a match saved by <see cref="Save"/>. Replays the
        /// command log to rebuild the event log, then asserts the replayed
        /// state hash matches the stored state (integrity check).</summary>
        public static MatchRunner Load(string saved, IDiceProvider dice)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            string[] parts = saved.Split(new[] { "\n" + MatchCodec.CommandLogHeader + "\n" }, 2, StringSplitOptions.None);
            if (parts.Length != 2) throw new FormatException("save data missing command log");
            GameState state = MatchCodec.LoadState(parts[0]);

            string[] logs = parts[1].Split(new[] { "\n" + MatchCodec.EventLogHeader + "\n" }, 2, StringSplitOptions.None);
            if (logs.Length != 2) throw new FormatException("save data missing event log");

            var runner = new MatchRunner(state.Config, dice);
            runner._state = state;
            foreach (string line in NonEmpty(logs[0])) runner._commandLog.Add(CommandParser.Parse(line));
            foreach (string line in NonEmpty(logs[1])) runner._eventLog.Add(EventParser.Parse(line));

            // integrity: replay must reproduce the exact state
            GameState replayed = Simulation.Replay(state.Config, runner._commandLog, out var events);
            if (replayed.StateHash() != state.StateHash())
                throw new FormatException("save data integrity failure: replay hash mismatch");
            return runner;
        }

        static IEnumerable<string> NonEmpty(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                string t = line.Trim('\r');
                if (t.Length > 0) yield return t;
            }
        }

        static string[] ConvertAll<T>(IReadOnlyList<T> items, Func<T, string> line)
        {
            var result = new string[items.Count];
            for (int i = 0; i < items.Count; i++) result[i] = line(items[i]);
            return result;
        }

        static string[] ConvertAll(List<Command> items) => ConvertAll((IReadOnlyList<Command>)items, c => c.Line());
        static string[] ConvertAll(List<GameEvent> items) => ConvertAll((IReadOnlyList<GameEvent>)items, e => e.Line());
    }

    // =====================================================================

    public enum ControllerKind { Human, Bot }

    /// <summary>Match setup: seat count + who controls each seat + house
    /// rules (D4 blockade toggle). Pure data — the Unity layer exposes this
    /// in the match-setup screen; online rooms replicate it (M6).</summary>
    public sealed class MatchSetup
    {
        public int Players = 4;
        public bool Blockades = true;
        public bool ThreeSixes = true;
        public bool CaptureExtra = true;
        public bool Placings = true;
        public BotTier BotTier = BotTier.Hard;   // difficulty for all bot seats

        /// <summary>One entry per seat in play (seat order per Rules.Seats).</summary>
        public ControllerKind[] Controllers = { ControllerKind.Human, ControllerKind.Human, ControllerKind.Human, ControllerKind.Human };

        public MatchConfig BuildConfig()
        {
            return new MatchConfig(
                Seats.ForPlayerCount(Players),
                blockades: Blockades, threeSixes: ThreeSixes,
                captureExtra: CaptureExtra, placings: Placings);
        }

        public ControllerKind KindFor(Seat seat)
        {
            int order = Array.IndexOf(Seats.ForPlayerCount(Players), seat);
            if (order < 0 || Controllers == null) return ControllerKind.Human;
            return order < Controllers.Length ? Controllers[order] : ControllerKind.Human;
        }
    }
}

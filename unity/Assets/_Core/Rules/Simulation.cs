using System;
using System.Collections.Generic;

namespace DesiLudo3D.Rules
{
    /// <summary>Deterministic random-match simulation (C# side). NOTE: the RNG
    /// sequence differs from the Python reference (different RNG algorithms) —
    /// cross-implementation equality is enforced by the conformance vectors,
    /// not by this driver. This is used for C#-side fuzz/self-consistency.</summary>
    public static class Simulation
    {
        public static void PlayRandomMatch(MatchConfig config, int seed, int maxCommands,
                                           out GameState finalState,
                                           out List<Command> commandLog,
                                           out List<GameEvent> eventLog)
        {
            var rng = new Random(seed);
            GameState state = RulesEngine.NewMatch(config);
            commandLog = new List<Command>();
            eventLog = new List<GameEvent>();

            for (int i = 0; i < maxCommands; i++)
            {
                if (state.Phase == TurnPhase.MatchOver)
                {
                    finalState = state;
                    return;
                }
                Command cmd;
                if (state.Phase == TurnPhase.AwaitRoll)
                {
                    cmd = new RollDiceCommand(state.CurrentSeat, rng.Next(1, 7));
                }
                else
                {
                    List<LegalMove> moves = RulesEngine.GetLegalMoves(state, state.CurrentSeat, state.LastDie);
                    if (moves.Count == 0)
                        throw new InvalidOperationException("engine invariant broken: AwaitMove with no legal moves");
                    cmd = new MoveTokenCommand(state.CurrentSeat, moves[rng.Next(moves.Count)].TokenIndex);
                }
                ApplyResult res = RulesEngine.Apply(state, cmd);
                if (!res.Ok)
                    throw new InvalidOperationException("driver produced illegal command " + cmd.Line() + ": " + res.Error);
                commandLog.Add(cmd);
                eventLog.AddRange(res.Events);
                state = res.NewState;
            }
            throw new TimeoutException("match did not finish within " + maxCommands + " commands");
        }

        public static GameState Replay(MatchConfig config, IEnumerable<Command> commands,
                                       out List<GameEvent> events)
        {
            GameState state = RulesEngine.NewMatch(config);
            events = new List<GameEvent>();
            foreach (Command cmd in commands)
            {
                ApplyResult res = RulesEngine.Apply(state, cmd);
                if (!res.Ok)
                    throw new InvalidOperationException("replay hit illegal command " + cmd.Line() + ": " + res.Error);
                events.AddRange(res.Events);
                state = res.NewState;
            }
            return state;
        }

        public static bool VerifyReplay(MatchConfig config, List<Command> commands,
                                        GameState finalState, List<GameEvent> finalEvents)
        {
            GameState state = Replay(config, commands, out var events);
            if (state.StateHash() != finalState.StateHash()) return false;
            if (events.Count != finalEvents.Count) return false;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Line() != finalEvents[i].Line()) return false;
            return true;
        }
    }
}

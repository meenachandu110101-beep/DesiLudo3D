using System.Collections.Generic;
using DesiLudo3D.Rules;

namespace DesiLudo3D.Core
{
    /// <summary>One comedy reaction: which seat speaks, in what context,
    /// with which R1 emotion tag, anchored to which token (-1 = none).</summary>
    public struct ComedyReaction
    {
        public int SeatIndex;
        public ComedyContext Context;
        public string Emotion;
        public int TokenIndex;

        public ComedyReaction(int seatIndex, ComedyContext context, string emotion,
                              int tokenIndex = -1)
        {
            SeatIndex = seatIndex;
            Context = context;
            Emotion = emotion;
            TokenIndex = tokenIndex;
        }
    }

    /// <summary>GameEvent -> comedy reactions (0, 1 or 2). C# mirror of the
    /// VALIDATED Python reference (ludo.comedy.reactions_for_event).
    /// Ordinary moves and turn ends are silent by design (anti-chatter);
    /// captures produce BOTH perspectives (attacker + victim).</summary>
    public static class ComedyEvents
    {
        public static List<ComedyReaction> FromGameEvent(GameEvent e,
                                                          IReadOnlyList<Seat> seatsInPlay)
        {
            var outReactions = new List<ComedyReaction>(2);

            switch (e)
            {
                case DiceRolled d:
                    if (d.Die == 6)
                        Add(outReactions, seatsInPlay, d.Seat, ComedyContext.Six);
                    else if (d.WasExtra)
                        Add(outReactions, seatsInPlay, d.Seat, ComedyContext.Extra);
                    break;

                case NoLegalMove n:
                    Add(outReactions, seatsInPlay, n.Seat, ComedyContext.NoMove);
                    break;

                case TokenMoved m:
                    if (m.IsRelease)
                        Add(outReactions, seatsInPlay, m.Seat, ComedyContext.Release,
                            m.TokenIndex);
                    break;

                case TokenCaptured c:
                    Add(outReactions, seatsInPlay, c.AttackerSeat, ComedyContext.Capture,
                        c.AttackerIndex);
                    Add(outReactions, seatsInPlay, c.VictimSeat, ComedyContext.Captured,
                        c.VictimIndex);
                    break;

                case TokenFinished f:
                    Add(outReactions, seatsInPlay, f.Seat, ComedyContext.Finish,
                        f.TokenIndex);
                    break;

                case PlayerFinished p:
                    Add(outReactions, seatsInPlay, p.Seat,
                        p.Place == 1 ? ComedyContext.Victory : ComedyContext.Place);
                    break;

                case TurnVoided v:
                    Add(outReactions, seatsInPlay, v.Seat, ComedyContext.Void);
                    break;

                case MatchEnded end:
                    Seat winner = end.Standings != null && end.Standings.Length > 0
                        ? end.Standings[0].Seat
                        : (Seat)(-1);
                    for (int i = 0; i < seatsInPlay.Count; i++)
                    {
                        bool won = seatsInPlay[i] == winner;
                        outReactions.Add(new ComedyReaction(
                            i,
                            won ? ComedyContext.Victory : ComedyContext.Defeat,
                            won ? ComedyPools.Emotions[(int)ComedyContext.Victory]
                                : ComedyPools.Emotions[(int)ComedyContext.Defeat],
                            -1));
                    }
                    break;

                // PlayerLeft / TurnEnded: silent in V1 (Leave pool reserved).
            }

            return outReactions;
        }

        static void Add(List<ComedyReaction> list, IReadOnlyList<Seat> seats, Seat seat,
                        ComedyContext context, int tokenIndex = -1)
        {
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i] != seat) continue;
                list.Add(new ComedyReaction(i, context,
                    ComedyPools.Emotions[(int)context], tokenIndex));
                return;
            }
        }
    }
}

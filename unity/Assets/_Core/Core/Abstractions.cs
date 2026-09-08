using System;
using System.Collections.Generic;
using DesiLudo3D.Rules;

namespace DesiLudo3D.Core
{
    // =====================================================================
    // Provider seams (spec §11). These are the future-AI / future-service
    // boundaries: V1 ships simple local implementations; later versions can
    // add smarter/online variants without touching gameplay code.
    // =====================================================================

    /// <summary>Source of die values. Local matches use a seeded RNG;
    /// online matches (M6) will use commit–reveal (Rules.CommitRevealDice).</summary>
    public interface IDiceProvider
    {
        int RollDie();
    }

    /// <summary>Bot brain seam (spec §11 OpponentAgent). M1 ships a random
    /// placeholder; M4 adds heuristic + expectiminimax tiers behind the same
    /// interface; a future learned agent slots in here too.</summary>
    public interface IOpponentAgent
    {
        string Name { get; }

        /// <summary>Choose one of the legal moves for the current turn.</summary>
        LegalMove ChooseMove(GameState state, IReadOnlyList<LegalMove> moves);
    }

    /// <summary>Reaction presentation seam (spec §5.2 FaceReactionProvider).
    /// V1 = R1 (picker-imported prerecorded clips) + bubbles + voice.
    /// Camera-based layers (R2/R3) are post-V1 (Amendment A1) and must be
    /// addable WITHOUT touching gameplay code — this is that boundary.</summary>
    public interface IFaceReactionProvider
    {
        /// <summary>True if this provider can render a reaction for the seat.</summary>
        bool CanReact(int seatIndex);

        /// <summary>Play the given reaction emotion for the seat (e.g.
        /// Laugh, Angry, Surprise, TongueOut, Sad, Victory — ReactionDirector
        /// maps events to emotions; presentation decides the medium).</summary>
        void PlayReaction(int seatIndex, string emotion);
    }

    /// <summary>Networking seam (spec §9 / NetTransport). M5 introduces the
    /// LAN implementation; M6 the internet provider (EOS or Nakama, D6).
    /// Gameplay code only ever sees this interface.</summary>
    public interface INetTransport
    {
        bool IsConnected { get; }

        /// <summary>Send a player intent command to the authoritative host.</summary>
        void SendCommand(Command command);

        /// <summary>Host: broadcast an authoritative event batch to guests.</summary>
        void BroadcastEvents(IReadOnlyList<GameEvent> events);

        event Action<Command> CommandReceived;
        event Action<IReadOnlyList<GameEvent>> EventsReceived;
    }

    /// <summary>Device-local profile persistence seam (spec §12 ProfileStore).
    /// Photos, R1 clips, character type & voice profile — device-only in V1.</summary>
    public interface IProfileStore
    {
        string GetProfileJson(int seatIndex);
        void SaveProfileJson(int seatIndex, string json);
    }
}

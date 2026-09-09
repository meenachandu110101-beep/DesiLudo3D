using System;
using System.Collections.Generic;
using DesiLudo3D.Core;
using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Bridges the validated Core comedy engine to presentation:
    /// consumes the controller's animated-event stream, selects lines per
    /// seat identity (category/personality — A2/A3/A5: NEVER token colour),
    /// then presents each line as SPOKEN comedy (on-device TTS interim until
    /// M4 prerecorded VO) plus a readable bubble (A4). R1 clip overlay and
    /// save/resume hooks attach to the same events.</summary>
    public class ComedyPlayback : MonoBehaviour
    {
        PassAndPlayController _controller;
        BoardBuilder _board;
        ProfileService _profiles;
        ISpeechSynthesis _speech;
        ComedyDirector _director;

        struct Queued
        {
            public int Slot;
            public ComedyLine Line;
            public string Emotion;
            public TokenView Anchor;
        }

        readonly Queue<Queued> _queue = new Queue<Queued>(8);
        SpeechBubbleView _bubble;
        float _nextBubbleAt;

        /// <summary>Raised when a line is actually presented (R1 prerecorded
        /// clip overlay subscribes here — never camera, A1).</summary>
        public event Action<ComedyLine, string, int> LinePresented;

        public void Initialize(PassAndPlayController controller, BoardBuilder board,
                               ProfileService profiles, ISpeechSynthesis speech, ulong seed,
                               bool greet = true)
        {
            _controller = controller;
            _board = board;
            _profiles = profiles;
            _speech = speech ?? new StubSpeechSynthesis();
            _director = new ComedyDirector(seed);
            _queue.Clear();

            _controller.EventAnimated -= OnEventAnimated;
            _controller.EventAnimated += OnEventAnimated;

            // match-start greetings (cooldown-exempt; skipped when resuming
            // a match — the characters already greeted in the saved session)
            if (greet)
            {
                var seats = _controller.Runner.Config.SeatsInPlay;
                for (int i = 0; i < seats.Length; i++)
                {
                    var reaction = new ComedyReaction(i, ComedyContext.Greeting,
                        ComedyPools.Emotions[(int)ComedyContext.Greeting], -1);
                    TryQueue(reaction, immediate: i == 0);
                }
            }
        }

        void OnDestroy()
        {
            if (_controller != null) _controller.EventAnimated -= OnEventAnimated;
        }

        void OnEventAnimated(GameEvent e)
        {
            Seat[] seats = _controller.Runner.Config.SeatsInPlay;
            foreach (ComedyReaction r in ComedyEvents.FromGameEvent(e, seats))
                TryQueue(r, immediate: false);
        }

        void TryQueue(ComedyReaction r, bool immediate)
        {
            var profile = _profiles.Load(r.SeatIndex);
            var identity = profile.ToIdentity();
            if (!identity.ReactionsEnabled) return;

            ComedyLine line = _director.Select(r.SeatIndex, r.Context,
                identity.Category, identity.Personality, Time.time);
            if (line == null) return;   // cooldown or unknown context

            _queue.Enqueue(new Queued
            {
                Slot = r.SeatIndex,
                Line = line,
                Emotion = r.Emotion,
                Anchor = AnchorToken(r.SeatIndex, r.TokenIndex),
            });
            if (immediate) _nextBubbleAt = 0f;
            if (_queue.Count > 6) _queue.Dequeue();   // bound the backlog
        }

        TokenView AnchorToken(int seatIndex, int tokenIndex)
        {
            Seat[] seats = _controller.Runner.Config.SeatsInPlay;
            Seat seat = seats[seatIndex];
            TokenView best = null;
            if (tokenIndex >= 0 && tokenIndex < BoardGeometry.TokensPerSeat)
            {
                best = _board.Tokens[(int)seat * BoardGeometry.TokensPerSeat + tokenIndex];
            }
            else
            {
                // most advanced token of the seat (or first in base)
                int bestProgress = int.MinValue;
                for (int i = 0; i < BoardGeometry.TokensPerSeat; i++)
                {
                    var t = _board.Tokens[(int)seat * BoardGeometry.TokensPerSeat + i];
                    if (t == null) continue;
                    int p = _controller.Runner.State.Token(seat, i).Progress;
                    if (p > bestProgress) { bestProgress = p; best = t; }
                }
            }
            return best;
        }

        void Update()
        {
            if (_bubble != null) return;   // current bubble still on screen
            if (_queue.Count == 0) return;
            if (Time.time < _nextBubbleAt) return;

            Queued q = _queue.Dequeue();
            Present(q);
        }

        void Present(Queued q)
        {
            string text = UiStrings.Lang == UiLang.HI ? q.Line.Hi : q.Line.En;
            Transform anchor = q.Anchor != null ? q.Anchor.transform : transform;
            Color accent = q.Anchor != null
                ? BoardBuilder.ColorOf(q.Anchor.Seat)
                : Color.white;
            float duration = Mathf.Clamp(1.6f + 0.045f * text.Length, 2.2f, 3.4f);
            _bubble = SpeechBubbleView.Show(anchor, text, duration, accent);
            _nextBubbleAt = Time.time + duration + 0.9f;

            // spoken comedy is primary (A4): TTS interim until M4 VO assets;
            // VO playback will replace this call when a profile has assets.
            if (_speech.IsAvailable)
                _speech.Speak(text, UiStrings.SpeechTag);

            LinePresented?.Invoke(q.Line, q.Emotion, q.Slot);
        }
    }
}

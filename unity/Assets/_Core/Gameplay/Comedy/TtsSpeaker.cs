using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>On-device speech synthesis seam (A4). Free, offline, system
    /// TTS ONLY — this is the optional fallback layer: prerecorded VO assets
    /// (M4) always take precedence when available; the bubble text is always
    /// shown alongside (speech is primary, text is the accessibility
    /// surface — never text-only).</summary>
    public interface ISpeechSynthesis
    {
        bool IsAvailable { get; }

        /// <summary>Speak plain text in the given BCP-47 tag ("en-IN"/"hi-IN").</summary>
        void Speak(string text, string languageTag);

        void Stop();
    }

    /// <summary>Honest no-op fallback (Editor, unsupported platforms).</summary>
    public sealed class StubSpeechSynthesis : ISpeechSynthesis
    {
        public bool IsAvailable => false;
        public void Speak(string text, string languageTag) { }
        public void Stop() { }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Android system TextToSpeech via the JNI bridge — no plugin,
    /// no permissions, no network (android.speech.tts is an OS service).
    /// Initialization is async; utterances are queued until ready. All JNI
    /// failures degrade to unavailable (bubbles still carry the comedy).</summary>
    public sealed class AndroidTtsSpeaker : ISpeechSynthesis, IDisposable
    {
        AndroidJavaObject _tts;
        volatile bool _ready;
        readonly Queue<(string text, string lang)> _pending =
            new Queue<(string, string)>();
        string _lastLang;

        public bool IsAvailable => _ready;

        public AndroidTtsSpeaker()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    var listener = new InitListener(status =>
                    {
                        _ready = status == 0 /* TTS.SUCCESS */;
                        if (_ready) FlushPending();
                    });
                    _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech",
                        activity, listener);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] TTS unavailable: " + e.Message);
                _tts = null;
            }
        }

        class InitListener : AndroidJavaProxy
        {
            readonly Action<int> _onInit;
            public InitListener(Action<int> onInit)
                : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                _onInit = onInit;
            }

            void onInit(int status) => _onInit(status);
        }

        public void Speak(string text, string languageTag)
        {
            if (_tts == null || string.IsNullOrEmpty(text)) return;
            if (!_ready)
            {
                _pending.Enqueue((text, languageTag));
                if (_pending.Count > 4) _pending.Dequeue();   // bound the queue
                return;
            }
            try
            {
                if (languageTag != _lastLang)
                {
                    string lang = languageTag ?? "en-IN";
                    string baseLang = lang;
                    string region = "";
                    int dash = lang.IndexOf('-');
                    if (dash > 0)
                    {
                        baseLang = lang.Substring(0, dash);
                        region = lang.Substring(dash + 1);
                    }
                    using (var locale = region.Length > 0
                        ? new AndroidJavaObject("java.util.Locale", baseLang, region)
                        : new AndroidJavaObject("java.util.Locale", baseLang))
                    {
                        _tts.Call<AndroidJavaObject>("setLanguage", locale);
                    }
                    _lastLang = languageTag;
                }
                // QUEUE_FLUSH = 1 (drop any in-flight utterance)
                _tts.Call("speak", text, 1, null, "desiludo_" + Guid.NewGuid().ToString("N"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] TTS speak failed: " + e.Message);
                _ready = false;
            }
        }

        void FlushPending()
        {
            while (_pending.Count > 0)
            {
                var (text, lang) = _pending.Dequeue();
                Speak(text, lang);
            }
        }

        public void Stop()
        {
            if (_tts == null) return;
            try { _tts.Call("stop"); }
            catch (Exception) { }
        }

        public void Dispose()
        {
            if (_tts == null) return;
            try { _tts.Call("shutdown"); _tts.Dispose(); }
            catch (Exception) { }
            _tts = null;
            _ready = false;
        }
    }
#endif

    public static class SpeechSynthesis
    {
        public static ISpeechSynthesis Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidTtsSpeaker();
#else
            return new StubSpeechSynthesis();
#endif
        }
    }
}

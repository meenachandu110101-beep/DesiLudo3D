using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    public enum UiLang { EN = 0, HI = 1 }

    /// <summary>EN/HI UI string table (D8: English + Hindi). One curated
    /// source for every player-facing string; the language toggle swaps live.
    /// Devanagari renders through Unity's dynamic-font OS fallback (Android
    /// ships Noto Devanagari) — device shaping quality is a checklist item;
    /// if uGUI Text shaping proves inadequate, M4 swaps the text layer to
    /// Text Core behind this same table.</summary>
    public static class UiStrings
    {
        static bool _loaded;
        static UiLang _lang;

        public static UiLang Lang
        {
            get
            {
                if (!_loaded)
                {
                    _lang = PlayerPrefs.GetInt("desiludo.lang", 0) == 1
                        ? UiLang.HI : UiLang.EN;
                    _loaded = true;
                }
                return _lang;
            }
        }

        public static void ToggleLang()
        {
            SetLang(Lang == UiLang.EN ? UiLang.HI : UiLang.EN);
        }

        public static void SetLang(UiLang lang)
        {
            _lang = lang;
            _loaded = true;
            PlayerPrefs.SetInt("desiludo.lang", (int)lang);
            PlayerPrefs.Save();
        }

        /// <summary>Language tag for speech synthesis ("en-IN"/"hi-IN").</summary>
        public static string SpeechTag => Lang == UiLang.HI ? "hi-IN" : "en-IN";

        public static string Get(string key)
        {
            return Entry(key)[(int)Lang];
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(Entry(key)[(int)Lang], args);
        }

        static string[] Entry(string key)
        {
            if (Table.TryGetValue(key, out var v)) return v;
            return new[] { "[" + key + "]", "[" + key + "]" };
        }

        static readonly System.Collections.Generic.Dictionary<string, string[]> Table =
            new System.Collections.Generic.Dictionary<string, string[]>
        {
            // HUD
            { "title",        new[] { "DesiLudo3D", "देसी लूडो 3डी" } },
            { "turn",         new[] { "{0}'s turn", "{0} की बारी" } },
            { "turnBot",      new[] { "{0} (bot)", "{0} (बॉट)" } },
            { "roll",         new[] { "ROLL", "पासा फेंको" } },
            { "tapToken",     new[] { "TAP A TOKEN", "गोली चुनो" } },
            { "playAgain",    new[] { "PLAY AGAIN", "फिर से खेलें" } },
            { "matchOver",    new[] { "MATCH OVER!", "मैच ख़त्म!" } },
            { "profiles",     new[] { "PROFILES", "प्रोफ़ाइल" } },
            { "close",        new[] { "CLOSE", "बंद करें" } },
            { "homeCount",    new[] { "({0} home)", "({0} घर)" } },
            { "langName",     new[] { "अ", "A" } },   // shows the other language

            // ticker
            { "noMove",       new[] { "{0} has no legal move ({1})", "{0} के पास कोई चाल नहीं ({1})" } },
            { "cut",          new[] { "{0} cut {1}'s token!", "{0} ने {1} की गोली काट दी!" } },
            { "tokenHome",    new[] { "{0}'s token reached home!", "{0} की गोली घर पहुँची!" } },
            { "playerFinish", new[] { "{0} FINISHED — place {1}!", "{0} फ़िनिश — नंबर {1}!" } },
            { "voided",       new[] { "Three sixes! {0}'s turn is VOID!", "तीन छह! {0} की बारी रद्द!" } },
            { "left",         new[] { "{0} left the match", "{0} मैच छोड़ गए" } },
            { "illegal",      new[] { "Illegal: {0}", "ग़लत चाल: {0}" } },
            { "extraRoll",    new[] { "Extra roll for {0}!", "{0} को एक और मौका!" } },

            // identity center
            { "profilesTitle", new[] { "PROFILES — who is playing?", "प्रोफ़ाइल — कौन खेल रहा है?" } },
            { "voice",        new[] { "VOICE: {0}", "आवाज़: {0}" } },
            { "style",        new[] { "STYLE: {0}", "स्टाइल: {0}" } },
            { "pickPhoto",    new[] { "PICK PHOTO", "फ़ोटो चुनें" } },
            { "replacePhoto", new[] { "REPLACE PHOTO", "फ़ोटो बदलें" } },
            { "clear",        new[] { "CLEAR", "हटाएँ" } },
            { "botTag",       new[] { "BOT", "बॉट" } },
            { "catMale",      new[] { "MALE", "पुरुष" } },
            { "catFemale",    new[] { "FEMALE", "महिला" } },
            { "catChild",     new[] { "CHILD", "बच्चा" } },
            { "styBalanced",  new[] { "BALANCED", "संतुलित" } },
            { "styOverconfident", new[] { "OVERCONFIDENT", "आत्मविश्वासी" } },
            { "styDrama",     new[] { "DRAMA KING", "ड्रामा किंग" } },
            { "stySweet",     new[] { "SWEET-BUT-DEADLY", "मीठा-पर-ख़तरनाक" } },
            { "styExcited",   new[] { "EXCITED", "जोशीला" } },

            // clips (R1 — prerecorded, picker-imported; never camera — A1)
            { "clips",        new[] { "CLIPS", "क्लिप्स" } },
            { "clipsTitle",   new[] { "REACTION CLIPS — prerecorded only", "रिएक्शन क्लिप्स — सिर्फ़ पहले से रिकॉर्ड की हुई" } },
            { "pickClip",     new[] { "PICK CLIP", "क्लिप चुनें" } },
            { "clipFor",      new[] { "{0}: {1}", "{0}: {1}" } },
            { "remove",       new[] { "REMOVE", "हटाएँ" } },
            { "done",         new[] { "DONE", "हो गया" } },

            // save / resume
            { "resumeTitle",  new[] { "Resume last match?", "पिछला मैच जारी रखें?" } },
            { "resume",       new[] { "RESUME", "जारी रखें" } },
            { "newMatch",     new[] { "NEW MATCH", "नया मैच" } },
        };
    }
}

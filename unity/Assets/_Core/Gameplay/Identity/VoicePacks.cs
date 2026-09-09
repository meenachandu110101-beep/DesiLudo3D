using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DesiLudo3D.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Prerecorded VO acquisition (A1-safe: picker only — the owner
    /// records VO clips outside the game and imports them; no microphone
    /// permission, no camera, no recording in-app).</summary>
    public interface IAudioImporter
    {
        bool IsAvailable { get; }
        void ImportAudio(Action<string> onResult);
    }

    public sealed class EditorAudioImporter : IAudioImporter
    {
        public bool IsAvailable => true;

        public void ImportAudio(Action<string> onResult)
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                "Choose a voice clip", "", new[] { "Audio", "wav,ogg,mp3" });
            onResult(string.IsNullOrEmpty(path) ? null : path);
#else
            onResult(null);
#endif
        }
    }

#if NATIVE_GALLERY && UNITY_ANDROID && !UNITY_EDITOR
    public sealed class NativeGalleryAudioImporter : IAudioImporter
    {
        public bool IsAvailable => true;

        public void ImportAudio(Action<string> onResult)
        {
            NativeGallery.GetAudioFromGallery(
                path => onResult(string.IsNullOrEmpty(path) ? null : path));
        }
    }
#endif

    public sealed class StubAudioImporter : IAudioImporter
    {
        public bool IsAvailable => false;
        public void ImportAudio(Action<string> onResult) => onResult(null);
    }

    public static class AudioImporters
    {
        public static IAudioImporter Create()
        {
#if NATIVE_GALLERY && UNITY_ANDROID && !UNITY_EDITOR
            return new NativeGalleryAudioImporter();
#elif UNITY_EDITOR
            return new EditorAudioImporter();
#else
            return new StubAudioImporter();
#endif
        }
    }

    /// <summary>Voice packs: prerecorded VO per character category (A2: the
    /// PROFILE's category picks the voice — never the token colour) ×
    /// emotion. Files copied from the system picker into
    /// persistentDataPath/voice; loaded via UnityWebRequestMultimedia and
    /// cached. A4 precedence: prerecorded VO > system TTS > bubble-only.</summary>
    public class VoicePackLibrary
    {
        [Serializable]
        public class VoiceEntry
        {
            public string path = "";
            public string label = "";
        }

        [Serializable]
        class PackData
        {
            public List<string> categoryKeys = new List<string>();
            public List<string> entryJson = new List<string>();
        }

        readonly string _dir;
        readonly string _indexPath;
        // (category, emotion) -> file path
        readonly Dictionary<string, string> _paths = new Dictionary<string, string>();
        // (category, emotion) -> loaded clip
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        public static string[] Categories => new[] { "male", "female", "child" };
        public static readonly string[] Emotions = ReactionClipLibrary.Emotions;

        public VoicePackLibrary()
        {
            _dir = Path.Combine(Application.persistentDataPath, "voice");
            Directory.CreateDirectory(_dir);
            _indexPath = Path.Combine(_dir, "index.json");
            Load();
        }

        static string Key(CharacterCategory c, string emotion) =>
            CategoryId(c) + ":" + emotion;

        public static string CategoryId(CharacterCategory c) =>
            c == CharacterCategory.Female ? "female"
            : c == CharacterCategory.Child ? "child" : "male";

        public bool HasEntry(CharacterCategory category, string emotion) =>
            _paths.ContainsKey(Key(category, emotion));

        /// <summary>Cached clip or null (call PreloadAll at boot).</summary>
        public AudioClip GetClip(CharacterCategory category, string emotion)
        {
            return _clips.TryGetValue(Key(category, emotion), out var clip)
                ? clip : null;
        }

        public VoiceEntry GetEntry(CharacterCategory category, string emotion)
        {
            return _paths.TryGetValue(Key(category, emotion), out var p)
                ? new VoiceEntry { path = p, label = Path.GetFileName(p) } : null;
        }

        public bool Set(CharacterCategory category, string emotion, string sourcePath)
        {
            try
            {
                string ext = Path.GetExtension(sourcePath);
                if (string.IsNullOrEmpty(ext)) ext = ".wav";
                string dest = Path.Combine(_dir,
                    CategoryId(category) + "_" + emotion + ext);
                if (File.Exists(dest)) File.Delete(dest);
                File.Copy(sourcePath, dest, true);
                _paths[Key(category, emotion)] = dest;
                _clips.Remove(Key(category, emotion));
                Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] VO import failed: " + e.Message);
                return false;
            }
        }

        public void Remove(CharacterCategory category, string emotion)
        {
            string key = Key(category, emotion);
            if (_paths.TryGetValue(key, out var p))
            {
                try { if (File.Exists(p)) File.Delete(p); } catch (Exception) { }
            }
            _paths.Remove(key);
            _clips.Remove(key);
            Save();
        }

        /// <summary>Preload every registered entry into the clip cache.</summary>
        public IEnumerator PreloadAll(MonoBehaviour host)
        {
            foreach (var kv in _paths)
            {
                string key = kv.Key;
                if (_clips.ContainsKey(key)) continue;
                string url = "file://" + kv.Value;
                AudioType type = AudioTypeFor(kv.Value);
                using (var req = UnityWebRequestMultimedia.GetAudioClip(url, type))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var clip = DownloadHandlerAudioClip.GetContent(req);
                        if (clip != null) _clips[key] = clip;
                    }
                    else
                    {
                        Debug.LogWarning("[DesiLudo3D] VO load failed (" + key + "): "
                                         + req.error);
                    }
                }
            }
        }

        static AudioType AudioTypeFor(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".ogg": return AudioType.OGGVORBIS;
                case ".mp3": return AudioType.MPEG;
                default: return AudioType.WAV;
            }
        }

        void Save()
        {
            try
            {
                var data = new PackData();
                foreach (var kv in _paths)
                {
                    data.categoryKeys.Add(kv.Key);
                    data.entryJson.Add(JsonUtility.ToJson(
                        new VoiceEntry { path = kv.Value }));
                }
                File.WriteAllText(_indexPath, JsonUtility.ToJson(data));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] voice index save failed: " + e.Message);
            }
        }

        void Load()
        {
            try
            {
                if (!File.Exists(_indexPath)) return;
                var data = JsonUtility.FromJson<PackData>(File.ReadAllText(_indexPath));
                if (data == null) return;
                for (int i = 0; i < data.categoryKeys.Count && i < data.entryJson.Count; i++)
                {
                    var entry = JsonUtility.FromJson<VoiceEntry>(data.entryJson[i]);
                    if (entry != null && File.Exists(entry.path))
                        _paths[data.categoryKeys[i]] = entry.path;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] voice index load failed: " + e.Message);
            }
        }
    }

    /// <summary>Gray-box VO pack manager UI: three category tabs, one row per
    /// emotion — pick (system picker), preview, remove.</summary>
    public class VoicePackPanel : MonoBehaviour
    {
        VoicePackLibrary _lib;
        IAudioImporter _importer;
        AudioSource _preview;
        GameObject _panel;
        GameObject _rows;
        string _category = "male";
        bool _busy;

        public void Initialize(VoicePackLibrary library, IAudioImporter importer)
        {
            _lib = library;
            _importer = importer;
            _preview = gameObject.AddComponent<AudioSource>();
            _preview.playOnAwake = false;
            StartCoroutine(_lib.PreloadAll(this));
        }

        public void Open()
        {
            BuildPanel();
            _panel.SetActive(true);
        }

        void Close()
        {
            if (_preview != null) _preview.Stop();
            _panel.SetActive(false);
        }

        void BuildPanel()
        {
            if (_panel != null) { RebuildRows(); return; }
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _panel = new GameObject("VoicePanel", typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.02f, 0.96f);

            var title = MakeText(_panel.transform, "Title", "VOICE PACKS", 44,
                FontStyle.Bold, new Color(1f, 0.85f, 0.6f));
            Top(title.rectTransform, new Vector2(0f, -90f), new Vector2(1040f, 90f));

            // category tabs
            for (int i = 0; i < VoicePackLibrary.Categories.Length; i++)
            {
                string cat = VoicePackLibrary.Categories[i];
                var tab = MakeButton(_panel.transform, "Tab_" + cat, cat.ToUpper(),
                    new Color(0.25f, 0.35f, 0.55f), 32);
                var tr = tab.GetComponent<RectTransform>();
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
                tr.anchoredPosition = new Vector2((i - 1) * 320f, -190f);
                tr.sizeDelta = new Vector2(300f, 90f);
                tab.onClick.AddListener(() => { _category = cat; RebuildRows(); });
            }

            _rows = new GameObject("Rows", typeof(RectTransform));
            _rows.transform.SetParent(_panel.transform, false);
            _rows.GetComponent<RectTransform>().Stretch();
            RebuildRows();

            var done = MakeButton(_panel.transform, "DoneButton", UiStrings.Get("done"),
                new Color(0.2f, 0.5f, 0.3f), 44);
            var dr = done.GetComponent<RectTransform>();
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0f);
            dr.anchoredPosition = new Vector2(0f, 70f);
            dr.sizeDelta = new Vector2(360f, 110f);
            done.onClick.AddListener(Close);

            _panel.SetActive(false);
        }

        void RebuildRows()
        {
            if (_rows == null) return;
            for (int i = _rows.transform.childCount - 1; i >= 0; i--)
                Destroy(_rows.transform.GetChild(i).gameObject);

            var catEnum = _category == "female" ? CharacterCategory.Female
                        : _category == "child" ? CharacterCategory.Child
                        : CharacterCategory.Male;

            for (int i = 0; i < VoicePackLibrary.Emotions.Length; i++)
            {
                string emotion = VoicePackLibrary.Emotions[i];
                float y = -320f - i * 90f;

                var emoKey = "emo" + char.ToUpper(emotion[0]) + emotion.Substring(1);
                var label = MakeText(_rows.transform, "E_" + emotion,
                    UiStrings.Get(emoKey), 30, FontStyle.Bold, new Color(1f, 0.9f, 0.7f));
                Place(label.rectTransform, -370f, y, 180f, 70f);

                var pick = MakeButton(_rows.transform, "P_" + emotion,
                    UiStrings.Get(_lib.HasEntry(catEnum, emotion) ? "replace" : "pick"),
                    new Color(0.2f, 0.5f, 0.3f), 26);
                Place(pick.GetComponent<RectTransform>(), -160f, y, 200f, 70f);
                string emo = emotion;
                pick.onClick.AddListener(() => Pick(catEnum, emo));

                if (_lib.HasEntry(catEnum, emotion))
                {
                    var play = MakeButton(_rows.transform, "V_" + emotion, "▶",
                        new Color(0.25f, 0.35f, 0.55f), 30);
                    Place(play.GetComponent<RectTransform>(), 40f, y, 90f, 70f);
                    play.onClick.AddListener(() => Preview(catEnum, emo));

                    var rem = MakeButton(_rows.transform, "R_" + emotion,
                        UiStrings.Get("remove"), new Color(0.6f, 0.25f, 0.25f), 24);
                    Place(rem.GetComponent<RectTransform>(), 170f, y, 150f, 70f);
                    rem.onClick.AddListener(() =>
                    {
                        _lib.Remove(catEnum, emo);
                        RebuildRows();
                    });
                }
            }
        }

        void Pick(CharacterCategory category, string emotion)
        {
            if (_busy) return;
            if (!_importer.IsAvailable)
            {
                Debug.Log("[DesiLudo3D] Audio import unavailable on this platform.");
                return;
            }
            _busy = true;
            _importer.ImportAudio(path =>
            {
                _busy = false;
                if (string.IsNullOrEmpty(path)) return;
                if (_lib.Set(category, emotion, path))
                {
                    StartCoroutine(PreviewAndLoad(category, emotion));
                    RebuildRows();
                }
            });
        }

        IEnumerator PreviewAndLoad(CharacterCategory category, string emotion)
        {
            yield return StartCoroutine(_lib.PreloadAll(this));
            Preview(category, emotion);
        }

        void Preview(CharacterCategory category, string emotion)
        {
            var clip = _lib.GetClip(category, emotion);
            if (clip != null && _preview != null)
            {
                _preview.Stop();
                _preview.PlayOneShot(clip, 0.9f);
            }
        }

        // ------------------------------------------------------------ helpers

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Top(RectTransform rt, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        static Text MakeText(Transform parent, string name, string text, int size,
                             FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            return t;
        }

        static Button MakeButton(Transform parent, string name, string label,
                                 Color color, int fontSize)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            var text = MakeText(go.transform, "Label", label, fontSize, FontStyle.Bold, Color.white);
            text.rectTransform.Stretch();
            text.raycastTarget = false;
            return button;
        }
    }
}

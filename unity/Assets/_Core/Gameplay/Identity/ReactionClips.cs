using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DesiLudo3D.Gameplay
{
    /// <summary>R1 clip persistence (A1): prerecorded videos copied from the
    /// system picker into persistentDataPath/clips, tagged per profile slot
    /// + emotion. JSON index at persistentDataPath/clips/index.json.
    /// Local-only: no upload, no sharing, no camera.</summary>
    public class ReactionClipLibrary
    {
        public static readonly string[] Emotions =
            { "happy", "excited", "laugh", "angry", "sad", "victory", "tired" };

        [Serializable]
        public class ClipEntry
        {
            public string path = "";
            public float startSec = 0f;
            public float endSec = 0f;    // 0 = play to end
            public string label = "";
        }

        [Serializable]
        class SlotData
        {
            public List<string> emotionKeys = new List<string>();
            public List<ClipEntry> entries = new List<ClipEntry>();
        }

        [Serializable]
        class LibraryData
        {
            public List<int> slots = new List<int>();
            public List<string> slotJson = new List<string>();
        }

        readonly string _dir;
        readonly string _indexPath;
        readonly Dictionary<int, Dictionary<string, ClipEntry>> _clips =
            new Dictionary<int, Dictionary<string, ClipEntry>>();

        public ReactionClipLibrary()
        {
            _dir = Path.Combine(Application.persistentDataPath, "clips");
            Directory.CreateDirectory(_dir);
            _indexPath = Path.Combine(_dir, "index.json");
            Load();
        }

        public ClipEntry Get(int slot, string emotion)
        {
            return _clips.TryGetValue(slot, out var map)
                && map.TryGetValue(emotion, out var e) ? e : null;
        }

        /// <summary>Copy the picked file into app storage and tag it.</summary>
        public ClipEntry Set(int slot, string emotion, string sourcePath)
        {
            try
            {
                string ext = Path.GetExtension(sourcePath);
                if (string.IsNullOrEmpty(ext)) ext = ".mp4";
                string dest = Path.Combine(_dir,
                    "slot" + slot + "_" + emotion + ext);
                if (File.Exists(dest)) File.Delete(dest);
                File.Copy(sourcePath, dest, true);

                var entry = new ClipEntry
                {
                    path = dest,
                    startSec = 0f,
                    endSec = 0f,
                    label = Path.GetFileName(sourcePath),
                };
                if (!_clips.TryGetValue(slot, out var map))
                    _clips[slot] = map = new Dictionary<string, ClipEntry>();
                map[emotion] = entry;
                Save();
                return entry;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] clip import failed: " + e.Message);
                return null;
            }
        }

        public void Remove(int slot, string emotion)
        {
            if (!_clips.TryGetValue(slot, out var map)) return;
            if (map.TryGetValue(emotion, out var entry))
            {
                try { if (File.Exists(entry.path)) File.Delete(entry.path); }
                catch (Exception) { }
            }
            map.Remove(emotion);
            Save();
        }

        public void AdjustTrim(int slot, string emotion, float dStart, float dEnd)
        {
            var e = Get(slot, emotion);
            if (e == null) return;
            e.startSec = Mathf.Max(0f, e.startSec + dStart);
            if (e.endSec > 0f) e.endSec = Mathf.Max(e.startSec + 0.5f, e.endSec + dEnd);
            Save();
        }

        void Save()
        {
            try
            {
                var data = new LibraryData();
                foreach (var kv in _clips)
                {
                    var slot = new SlotData();
                    foreach (var e in kv.Value)
                    {
                        slot.emotionKeys.Add(e.Key);
                        slot.entries.Add(e.Value);
                    }
                    data.slots.Add(kv.Key);
                    data.slotJson.Add(JsonUtility.ToJson(slot));
                }
                File.WriteAllText(_indexPath, JsonUtility.ToJson(data));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] clip index save failed: " + e.Message);
            }
        }

        void Load()
        {
            try
            {
                if (!File.Exists(_indexPath)) return;
                var data = JsonUtility.FromJson<LibraryData>(File.ReadAllText(_indexPath));
                if (data == null) return;
                for (int i = 0; i < data.slots.Count && i < data.slotJson.Count; i++)
                {
                    var slot = JsonUtility.FromJson<SlotData>(data.slotJson[i]);
                    if (slot == null) continue;
                    var map = new Dictionary<string, ClipEntry>();
                    for (int k = 0; k < slot.emotionKeys.Count && k < slot.entries.Count; k++)
                        if (File.Exists(slot.entries[k].path))
                            map[slot.emotionKeys[k]] = slot.entries[k];
                    _clips[data.slots[i]] = map;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] clip index load failed: " + e.Message);
            }
        }
    }

    /// <summary>Plays a profile's prerecorded clip for an emotion in a
    /// corner card (VideoPlayer -> RenderTexture -> RawImage). Local file
    /// playback only — A1: never camera, never network.</summary>
    public class ClipReactionOverlay : MonoBehaviour
    {
        VideoPlayer _player;
        RenderTexture _rt;
        GameObject _card;
        RawImage _image;
        Coroutine _running;

        public void Initialize()
        {
            var canvasGo = new GameObject("ClipOverlayCanvas", typeof(Canvas),
                                          typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _card = new GameObject("ClipCard", typeof(Image));
            _card.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)_card.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -260f);
            rect.sizeDelta = new Vector2(560f, 340f);
            _card.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.02f, 0.92f);

            _rt = new RenderTexture(512, 320, 0);
            _image = _card.AddComponent<RawImage>();
            _image.texture = _rt;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _rt;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.playOnAwake = false;

            _card.SetActive(false);
        }

        /// <summary>Play the slot's clip for the emotion (if any) for its
        /// trimmed duration. Returns true when a clip started.</summary>
        public bool PlayForSlot(ReactionClipLibrary library, int slot, string emotion)
        {
            var entry = library.Get(slot, emotion);
            if (entry == null || !File.Exists(entry.path)) return false;
            if (_player == null) return false;

            if (_running != null) StopCoroutine(_running);
            _card.SetActive(true);
            _player.url = "file://" + entry.path;
            _player.Play();
            _running = StartCoroutine(WatchEntry(entry));
            return true;
        }

        IEnumerator WatchEntry(ReactionClipLibrary.ClipEntry entry)
        {
            float end = entry.endSec > entry.startSec ? entry.endSec : float.MaxValue;

            // wait for prepare
            float waited = 0f;
            while (!_player.isPrepared && !_player.isPlaying && waited < 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!_player.isPlaying && !_player.isPrepared)
            {
                Hide();
                yield break;
            }

            if (entry.startSec > 0f)
            {
                _player.time = entry.startSec;
                while (!_player.isPlaying && Time.unscaledDeltaTime >= 0f
                       && !_player.isPrepared)
                    yield return null;
            }

            while (_player.isPlaying && _player.time < end)
                yield return null;

            _player.Stop();
            Hide();
        }

        void Hide()
        {
            if (_card != null) _card.SetActive(false);
            _running = null;
        }
    }
}

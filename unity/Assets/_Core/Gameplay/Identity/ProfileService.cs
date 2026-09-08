using System;
using System.IO;
using DesiLudo3D.Core;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Device-only profile persistence (spec §12): JSON files under
    /// persistentDataPath/profiles. Implements the Core IProfileStore seam.</summary>
    public sealed class FileProfileStore : IProfileStore
    {
        readonly string _dir;

        public FileProfileStore()
        {
            _dir = Path.Combine(Application.persistentDataPath, "profiles");
            Directory.CreateDirectory(_dir);
        }

        string PathFor(int slot) => Path.Combine(_dir, "profile_" + slot + ".json");

        public string GetProfileJson(int slot)
        {
            string p = PathFor(slot);
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        public void SaveProfileJson(int slot, string json)
        {
            File.WriteAllText(PathFor(slot), json ?? "");
        }

        public void DeleteProfileJson(int slot)
        {
            string p = PathFor(slot);
            if (File.Exists(p)) File.Delete(p);
        }
    }

    /// <summary>JSON-serializable profile payload (UnityEngine.JsonUtility
    /// format). The pure PlayerIdentity lives in Core; this wraps it for
    /// persistence plus the photo medallion path.</summary>
    [Serializable]
    public sealed class PlayerProfileData
    {
        public string name = "Player";
        public int category;             // CharacterCategory
        public int personality;          // PersonalityStyle
        public string voiceProfileId = "";
        public bool reactionsEnabled = true;
        public string photoPath = "";    // persisted oval-masked medallion PNG

        public PlayerIdentity ToIdentity()
        {
            return new PlayerIdentity
            {
                Name = name,
                Category = (CharacterCategory)category,
                Personality = (PersonalityStyle)personality,
                VoiceProfileId = voiceProfileId,
                ReactionsEnabled = reactionsEnabled,
            };
        }

        public static PlayerProfileData From(PlayerIdentity identity, string photoPath)
        {
            return new PlayerProfileData
            {
                name = identity?.Name ?? "Player",
                category = (int)(identity?.Category ?? CharacterCategory.Male),
                personality = (int)(identity?.Personality ?? PersonalityStyle.Balanced),
                voiceProfileId = identity?.VoiceProfileId ?? "",
                reactionsEnabled = identity?.ReactionsEnabled ?? true,
                photoPath = photoPath ?? "",
            };
        }
    }

    /// <summary>Manages local profiles (spec §5.1: up to 8, device-only,
    /// view/replace/delete). Gray-box M2 maps profile slots 0..3 onto the
    /// seats in play; the M3 profile editor screen generalizes this.</summary>
    public sealed class ProfileService
    {
        public const int MaxProfiles = 8;

        readonly IProfileStore _store;

        public ProfileService(IProfileStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public PlayerProfileData Load(int slot)
        {
            if (slot < 0 || slot >= MaxProfiles) throw new ArgumentOutOfRangeException(nameof(slot));
            string json = _store.GetProfileJson(slot);
            if (string.IsNullOrEmpty(json)) return new PlayerProfileData();
            try
            {
                var data = JsonUtility.FromJson<PlayerProfileData>(json);
                return data ?? new PlayerProfileData();
            }
            catch (ArgumentException)
            {
                return new PlayerProfileData();
            }
        }

        public void Save(int slot, PlayerProfileData profile)
        {
            if (slot < 0 || slot >= MaxProfiles) throw new ArgumentOutOfRangeException(nameof(slot));
            _store.SaveProfileJson(slot, JsonUtility.ToJson(profile));
        }

        /// <summary>Set/replace the photo (already masked + persisted path).</summary>
        public void SetPhoto(int slot, string medallionPath)
        {
            var profile = Load(slot);
            profile.photoPath = medallionPath ?? "";
            Save(slot, profile);
        }

        /// <summary>Delete the photo (spec §12: delete control).</summary>
        public void ClearPhoto(int slot)
        {
            var profile = Load(slot);
            profile.photoPath = "";
            Save(slot, profile);
            string path = Path.Combine(Application.persistentDataPath, "identity",
                                       "face_" + slot + ".png");
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

using System;
using UnityEngine;
#if NATIVE_GALLERY && UNITY_ANDROID && !UNITY_EDITOR
// NativeGallery (com.yasirkula.nativegallery, MIT) lives in the GLOBAL
// namespace; assembly name: NativeGallery.Runtime.
#endif

namespace DesiLudo3D.Gameplay
{
    /// <summary>Camera-free photo acquisition (Amendment A1): the ONLY paths
    /// are the system photo picker (device) or a file dialog (editor).
    /// Implementations must never open a camera or request storage
    /// permissions beyond what the system picker itself uses.</summary>
    public interface IPhotoImporter
    {
        bool IsAvailable { get; }

        /// <summary>Begin importing a photo. Callback receives the absolute
        /// file path of the picked image, or null on cancel/failure.</summary>
        void ImportPhoto(Action<string> onResult);
    }

    /// <summary>Editor path: Unity's file panel (validates the whole pipeline
    /// on desktop before any device pass).</summary>
    public sealed class EditorPhotoImporter : IPhotoImporter
    {
        public bool IsAvailable => true;

        public void ImportPhoto(Action<string> onResult)
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                "Choose a player photo", "", new[] { "Images", "png,jpg,jpeg" });
            onResult(string.IsNullOrEmpty(path) ? null : path);
#else
            onResult(null);
#endif
        }
    }

#if NATIVE_GALLERY && UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Device path: the free MIT NativeGallery plugin
    /// (com.yasirkula.nativegallery, v1.9.4). Uses ONLY the system photo
    /// picker — no camera; on Android 13+ no runtime permission at all.</summary>
    public sealed class NativeGalleryPhotoImporter : IPhotoImporter
    {
        public bool IsAvailable => true;

        public void ImportPhoto(Action<string> onResult)
        {
            // No permission gate: on Android 13+ the system Photo Picker is
            // permissionless (A1: no storage permission flow); on older
            // Android the plugin requests what it needs itself.
            NativeGallery.GetImageFromGallery(
                path => onResult(string.IsNullOrEmpty(path) ? null : path));
        }
    }
#endif

    /// <summary>Fallback (e.g., other platforms in M2): keeps the UI honest
    /// instead of pretending to work.</summary>
    public sealed class StubPhotoImporter : IPhotoImporter
    {
        public bool IsAvailable => false;
        public void ImportPhoto(Action<string> onResult) => onResult(null);
    }

    public static class PhotoImporters
    {
        /// <summary>Best available importer for the current platform.</summary>
        public static IPhotoImporter Create()
        {
#if NATIVE_GALLERY && UNITY_ANDROID && !UNITY_EDITOR
            return new NativeGalleryPhotoImporter();
#elif UNITY_EDITOR
            return new EditorPhotoImporter();
#else
            return new StubPhotoImporter();
#endif
        }
    }
}

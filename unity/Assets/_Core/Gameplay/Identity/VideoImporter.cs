using System;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>R1 prerecorded clip acquisition — Amendment A1: SYSTEM VIDEO
    /// PICKER ONLY. No camera, no recording, no capture, no sharing. The
    /// picked file is copied into app-private storage and played locally.</summary>
    public interface IVideoImporter
    {
        bool IsAvailable { get; }

        /// <summary>Begin importing a prerecorded video. Callback receives
        /// the absolute file path, or null on cancel/failure.</summary>
        void ImportVideo(Action<string> onResult);
    }

    /// <summary>Editor path: desktop file dialog.</summary>
    public sealed class EditorVideoImporter : IVideoImporter
    {
        public bool IsAvailable => true;

        public void ImportVideo(Action<string> onResult)
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                "Choose a prerecorded reaction clip", "",
                new[] { "Videos", "mp4,mov,webm,avi" });
            onResult(string.IsNullOrEmpty(path) ? null : path);
#else
            onResult(null);
#endif
        }
    }

#if NATIVE_GALLERY && UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Device path: system video picker via NativeGallery (MIT).
    /// Permissionless on Android 13+; never opens the camera (A1).</summary>
    public sealed class NativeGalleryVideoImporter : IVideoImporter
    {
        public bool IsAvailable => true;

        public void ImportVideo(Action<string> onResult)
        {
            NativeGallery.GetVideoFromGallery(
                path => onResult(string.IsNullOrEmpty(path) ? null : path));
        }
    }
#endif

    public sealed class StubVideoImporter : IVideoImporter
    {
        public bool IsAvailable => false;
        public void ImportVideo(Action<string> onResult) => onResult(null);
    }

    public static class VideoImporters
    {
        public static IVideoImporter Create()
        {
#if NATIVE_GALLERY && UNITY_ANDROID && !UNITY_EDITOR
            return new NativeGalleryVideoImporter();
#elif UNITY_EDITOR
            return new EditorVideoImporter();
#else
            return new StubVideoImporter();
#endif
        }
    }
}

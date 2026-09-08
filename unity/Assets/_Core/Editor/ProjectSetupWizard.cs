using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DesiLudo3D.EditorTools
{
    /// <summary>One-click project setup (docs/UNITY-VALIDATION.md). Creates
    /// correct project configuration WITHOUT hand-authored YAML: everything
    /// goes through Unity's own serialization via editor APIs, so it cannot
    /// corrupt settings. Run each menu item once after first project open.</summary>
    public static class ProjectSetupWizard
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        // --------------------------------------------------------------- 1

        [MenuItem("Tools/DesiLudo3D/1. Configure Player Settings")]
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "DesiLudo3D";
            PlayerSettings.productName = "DesiLudo3D";

            // D12: both orientations, portrait preferred
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Android (spec §10): min Android 8 / API 26; target auto (raise
            // to 36 at M7 submission per Play policy); IL2CPP + ARM64; GLES3.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Android, 2); // 2 = ARM64
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            TrySetActiveInputHandlerBoth();

            AssetDatabase.SaveAssets();
            Debug.Log("[DesiLudo3D] Player settings configured (Android, IL2CPP/ARM64, GLES3, both orientations).");
        }

        static void TrySetActiveInputHandlerBoth()
        {
            // GrayBoxInput compiles under any handler, but "Both" is the most
            // forgiving for the gray-box stage. Public API does not expose
            // this; attempt the internal setter, else instruct manually.
            try
            {
                var method = typeof(PlayerSettings).GetMethod("SetActiveInputHandler",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, new object[] { 2 }); // 2 = Both
                    Debug.Log("[DesiLudo3D] Active Input Handling set to Both.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DesiLudo3D] Could not set Active Input Handling automatically: " + ex.Message);
            }
            Debug.Log("[DesiLudo3D] NOTE: if input does not respond, set Project Settings > Player > Other Settings > Active Input Handling = Both.");
        }

        // --------------------------------------------------------------- 2

        [MenuItem("Tools/DesiLudo3D/2. Switch Build Target to Android")]
        public static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log("[DesiLudo3D] Already targeting Android.");
                return;
            }
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log(ok ? "[DesiLudo3D] Switched to Android."
                         : "[DesiLudo3D] Switch FAILED — switch manually in Build Settings.");
        }

        // --------------------------------------------------------------- 3

        [MenuItem("Tools/DesiLudo3D/3. Create Main Scene")]
        public static void CreateMainScene()
        {
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var boot = new GameObject("GameBootstrap");
            boot.AddComponent<DesiLudo3D.Gameplay.GameBootstrap>();

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError("[DesiLudo3D] Could not save scene to " + ScenePath);
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[DesiLudo3D] Main scene created at " + ScenePath + " and added to Build Settings. Press Play!");
        }

        // --------------------------------------------------------------- 4

        [MenuItem("Tools/DesiLudo3D/4. Assign URP Asset (if present)")]
        public static void AssignUrpAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            if (guids.Length == 0)
            {
                Debug.Log("[DesiLudo3D] No URP asset found. Create one: Project window > right-click > Create > Rendering > URP Asset (with Universal Renderer), name it e.g. 'DesiLudoURP', then re-run this menu item. (The gray-box also renders fine on Built-in until then.)");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                Debug.LogError("[DesiLudo3D] Found asset but it is not a UniversalRenderPipelineAsset: " + path);
                return;
            }

            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            AssetDatabase.SaveAssets();
            Debug.Log("[DesiLudo3D] URP asset assigned to Graphics & Quality settings: " + path);
        }
    }
}

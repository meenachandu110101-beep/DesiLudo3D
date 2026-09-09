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

            // Android (spec §10): min Android 8 / API 26; target API 36
            // (Play requirement for new apps post-Aug-2026); IL2CPP+ARM64;
            // GLES3. API 36 is set via reflection because older Unity 6
            // builds lack the AndroidApiLevel36 enum member — Auto is the
            // documented fallback (picks the highest installed SDK).
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            try
            {
                var field = typeof(AndroidSdkVersions).GetField("AndroidApiLevel36");
                if (field != null)
                    PlayerSettings.Android.targetSdkVersion =
                        (AndroidSdkVersions)field.GetValue(null);
                else
                    Debug.LogWarning("[DesiLudo3D] AndroidApiLevel36 not in this "
                        + "Unity build — keep Target = Automatic Installed, and "
                        + "verify API 36 in the Play upload pre-launch report.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] targetSdkVersion=36 not set: "
                    + e.Message);
            }

            // M7: version stamping (bump per release; code +1 every upload)
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Android, 2); // 2 = ARM64
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // Online play (M5): the P2P transport needs INTERNET in the
            // manifest (covers LAN/Wi-Fi sockets too). No other permissions:
            // no camera, no microphone, no storage (A1 + spec §12).
            // reflection: the InternetAccess enum's namespace moved between
            // Unity versions — this compiles and runs on all of them
            try
            {
                var prop = typeof(PlayerSettings.Android).GetProperty("internetAccess");
                if (prop != null)
                {
                    var enumType = prop.PropertyType;
                    prop.SetValue(null, System.Enum.Parse(enumType, "Require"), null);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] Could not set internetAccess automatically: "
                    + e.Message + " — set Player > Internet Access = Require manually.");
            }

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

        // --------------------------------------------------------------- 5

        [MenuItem("Tools/DesiLudo3D/5. Build Android AAB (release)")]
        public static void BuildAab()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogWarning("[DesiLudo3D] Switch to Android first (menu 2).");
                return;
            }
            // AAB with LZ4 compression (Play upload format; size budget §10:
            // target <= 80 MB). Debug symbols embedded is fine for V1.
            EditorUserBuildSettings.buildAppBundle = true;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/DesiLudo3D-" + PlayerSettings.bundleVersion
                                   + ".aab",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log(ok
                ? "[DesiLudo3D] AAB built: " + options.locationPathName + " ("
                  + (report.summary.totalSize / (1024 * 1024)) + " MB)"
                : "[DesiLudo3D] Build FAILED: " + report.summary.result
                  + " — see Console for errors.");
            if (ok && report.summary.totalSize > 120 * 1024 * 1024)
                Debug.LogWarning("[DesiLudo3D] AAB > 120 MB — spec §10 alert "
                                 + "threshold. Audit asset sizes before upload.");
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

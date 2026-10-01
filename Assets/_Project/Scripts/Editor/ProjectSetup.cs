using Squishy.Runtime.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Squishy.EditorTools
{
    /// <summary>
    /// Applies the portrait-mobile project settings, the URP asset (with the tilt-shift pass) and the Main scene.
    /// Runs once automatically on a fresh checkout, and any time from Squishy > Setup. Every step is safe to repeat.
    /// Batch mode: -executeMethod Squishy.EditorTools.ProjectSetup.Batch
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string PipelinePath = SettingsFolder + "/Mobile_URP.asset";
        private const string RendererPath = SettingsFolder + "/Mobile_URP_Renderer.asset";
        private const string TiltShiftMaterialPath = SettingsFolder + "/TiltShiftGrade.mat";
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (GraphicsSettings.defaultRenderPipeline != null) return;
                Debug.Log("Squishy: first open detected, running project setup.");
                RunAll();
            };
        }

        public static void Batch()
        {
            RunAll();
            EditorApplication.Exit(0);
        }

        /// <summary>The store app id (Google Play package name, App Store bundle id). It can never change after the first upload.</summary>
        public const string AppId = "com.naturaltwenty.squishiotchi";

        /// <summary>
        /// The Google Play release: a signed App Bundle (Builds/Squishiotchi.aab), version code raised by one, and admin
        /// tools forced off (SQUISHY_STORE). The upload key and its password live outside the project, in
        /// Documents/Squishiotchi signing/keystore.properties (or the folder in SQUISHY_SIGNING), never on GitHub.
        /// </summary>
        [MenuItem("Squishy/Build/Google Play bundle (release)")]
        public static void BuildAndroidRelease()
        {
            string dir = System.Environment.GetEnvironmentVariable("SQUISHY_SIGNING");
            if (string.IsNullOrEmpty(dir)) dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Squishiotchi signing");
            var propsPath = System.IO.Path.Combine(dir, "keystore.properties");
            if (!System.IO.File.Exists(propsPath)) { Debug.LogError("Squishy: no signing setup at " + propsPath); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            var props = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var line in System.IO.File.ReadAllLines(propsPath))
            {
                if (line.TrimStart().StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq > 0) props[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            AppIcon.Make();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = props["keystore"];
            PlayerSettings.Android.keystorePass = props["password"];
            PlayerSettings.Android.keyaliasName = props["alias"];
            PlayerSettings.Android.keyaliasPass = props["password"];
            PlayerSettings.Android.bundleVersionCode++;
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Master);
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.IsNullOrEmpty(defines) ? "SQUISHY_STORE" : defines + ";SQUISHY_STORE");
            UnityEditor.Build.Reporting.BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Builds/Squishiotchi.aab",
                    target = BuildTarget.Android,
                    options = BuildOptions.CompressWithLz4HC,
                });
            }
            finally
            {
                // Test APKs keep admin tools; the password isn't left in the project settings.
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, defines);
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                EditorUserBuildSettings.buildAppBundle = false;
                AssetDatabase.SaveAssets();
            }
            Debug.Log("Squishy: Google Play bundle " + report.summary.result + ", version code " + PlayerSettings.Android.bundleVersionCode + ", " + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " errors.");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Builds a test APK to Builds/Squishy.apk (debug-signed). Batch: -executeMethod Squishy.EditorTools.ProjectSetup.BuildAndroid</summary>
        [MenuItem("Squishy/Build/Android APK")]
        public static void BuildAndroid()
        {
            AppIcon.Make();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;
            // Keep test APKs small enough to send to a phone: strip unused engine code, compress tightly.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Master);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Squishy.apk",
                target = BuildTarget.Android,
                options = BuildOptions.CompressWithLz4HC,
            });
            Debug.Log("Squishy: Android build " + report.summary.result + ", " + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " errors.");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Web build to Builds/Web, for side-by-side comparison with the prototype in a browser.</summary>
        [MenuItem("Squishy/Build/Web (comparison)")]
        public static void BuildWeb()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Web", BuildTarget.WebGL, BuildOptions.None);
            Debug.Log("Squishy: Web build " + report.summary.result + ", " + report.summary.totalErrors + " errors.");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Squishy/Setup/Run Full Setup")]
        public static void RunAll()
        {
            ApplyPlayerSettings();
            CreateRenderPipeline();
            AddTiltShiftPass();
            CreateMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Squishy: setup done. Open " + ScenePath + " and press Play.");
        }

        [MenuItem("Squishy/Setup/Apply Player Settings")]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Natural Twenty on Re-entry";
            PlayerSettings.productName = "Squishiotchi";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetApplicationIdentifier(target, AppId); // permanent once uploaded to a store
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            }
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        [MenuItem("Squishy/Setup/Create Mobile Render Pipeline")]
        public static void CreateRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            // The prototype renders to an 8-bit target with 4x MSAA and grades in one pass; one shadowed key light.
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1f;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = 16f;
            pipeline.shadowCascadeCount = 1;
            pipeline.mainLightShadowmapResolution = 1024;
            EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        [MenuItem("Squishy/Setup/Add Tilt-Shift Pass")]
        public static void AddTiltShiftPass()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(TiltShiftMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Squishy/TiltShiftGrade"));
                AssetDatabase.CreateAsset(material, TiltShiftMaterialPath);
            }
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null) { Debug.LogWarning("Squishy: renderer missing."); return; }
            foreach (var f in rendererData.rendererFeatures)
                if (f is FullScreenPassRendererFeature fs && fs.passMaterial == material) return;
            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "TiltShiftGrade";
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing; // so URP's final blit encodes sRGB on every platform
            feature.fetchColorBuffer = true;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            var so = new SerializedObject(rendererData);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The scene is tiny: everything else is built at runtime by SteamerGame, like the prototype.</summary>
        [MenuItem("Squishy/Setup/Rebuild Main Scene")]
        public static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.81f, 0.70f, 0.55f);
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 80f;
            var data = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;

            var light = new GameObject("Key").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;

            new GameObject("Steamer Game").AddComponent<SteamerGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}

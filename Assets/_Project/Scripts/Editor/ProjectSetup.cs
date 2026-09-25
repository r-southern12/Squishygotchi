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
            PlayerSettings.companyName = "SquishyDumpling";
            PlayerSettings.productName = "Squishy Dumpling";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetApplicationIdentifier(target, "com.squishydumpling.game"); // placeholder: change before the first store upload
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
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
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

using Squishy.Data;
using Squishy.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Squishy.EditorTools
{
    /// <summary>
    /// Applies the portrait-mobile project settings, creates the URP asset and the Main scene.
    /// Runs once automatically on a fresh checkout (no render pipeline assigned yet), and can be
    /// re-run any time from Squishy > Setup. Every step is safe to repeat.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string PipelinePath = SettingsFolder + "/Mobile_URP.asset";
        private const string RendererPath = SettingsFolder + "/Mobile_URP_Renderer.asset";
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

        [MenuItem("Squishy/Setup/Run Full Setup")]
        public static void RunAll()
        {
            ContentSeeder.SeedFromSpec();
            ApplyPlayerSettings();
            CreateRenderPipeline();
            CreateMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Squishy: setup done. Open " + ScenePath + " and press Play.");
        }

        [MenuItem("Squishy/Setup/Apply Player Settings")]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "SquishyDumpling";
            PlayerSettings.productName = "Squishy Dumpling";

            // Portrait only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Placeholder bundle id: change before the first store upload.
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetApplicationIdentifier(target, "com.squishydumpling.game");
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            }
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        [MenuItem("Squishy/Setup/Create Mobile Render Pipeline")]
        public static void CreateRenderPipeline()
        {
            ContentSeeder.EnsureFolder(SettingsFolder);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                // Budget: one full-screen post effect, cheap lighting, adaptive resolution later.
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 2;
                pipeline.renderScale = 1f;
                pipeline.supportsCameraDepthTexture = true; // tilt-shift focus
                pipeline.supportsCameraOpaqueTexture = false;
                pipeline.shadowDistance = 20f;
                pipeline.shadowCascadeCount = 1;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            // Each quality level can override the pipeline; point them all at the same one.
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        [MenuItem("Squishy/Setup/Create Main Scene")]
        public static void CreateMainScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                ContentSeeder.EnsureFolder("Assets/_Project/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                var camera = cameraGo.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.95f, 0.90f, 0.82f); // cream
                camera.fieldOfView = 35f;
                cameraGo.AddComponent<UniversalAdditionalCameraData>();
                cameraGo.transform.position = new Vector3(0f, 6f, -9f);
                cameraGo.transform.rotation = Quaternion.Euler(32f, 0f, 0f);

                var lightGo = new GameObject("Sun");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.86f);
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                // Placeholder floor and squishy so Play shows something. Milestone 2 replaces them.
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                floor.name = "Placeholder_SteamerFloor";
                floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
                var squishy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                squishy.name = "Placeholder_Squishy";
                squishy.transform.position = new Vector3(0f, 0.55f, 0f);
                squishy.transform.localScale = new Vector3(1.1f, 0.9f, 1.1f);

                var game = new GameObject("Game");
                var boot = game.AddComponent<GameBootstrap>();
                var so = new SerializedObject(boot);
                so.FindProperty("content").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentSeeder.DatabasePath);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}

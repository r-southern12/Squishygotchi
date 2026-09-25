using Squishy.Data;
using Squishy.Runtime;
using Squishy.Runtime.CameraRig;
using Squishy.Runtime.Rendering;
using Squishy.Runtime.Room;
using Squishy.Runtime.SquishyPet;
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
                // New content types added in later milestones get seeded automatically.
                var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentSeeder.DatabasePath);
                if (db != null && db.care == null)
                {
                    Debug.Log("Squishy: new content types found, seeding missing assets.");
                    ContentSeeder.SeedFromSpec();
                }
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
            CreateMaterials();
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


        private const string MaterialsFolder = "Assets/_Project/Placeholder/Materials";
        private const string TiltShiftMaterialPath = SettingsFolder + "/TiltShiftGrade.mat";

        [MenuItem("Squishy/Setup/Create Materials")]
        public static void CreateMaterials()
        {
            ContentSeeder.EnsureFolder(MaterialsFolder);
            // Cheap matte material for all scenery; the squishy alone gets full Lit.
            Mat(MaterialsFolder + "/Scenery.mat", "Universal Render Pipeline/Simple Lit", m => m.SetFloat("_Smoothness", 0f));
            Mat(MaterialsFolder + "/Squishy.mat", "Universal Render Pipeline/Lit", m => m.SetFloat("_Smoothness", 0.5f));
            Mat(MaterialsFolder + "/Face.mat", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", new Color(0.2f, 0.15f, 0.13f)));
            Mat(TiltShiftMaterialPath, "Squishy/TiltShiftGrade", m => { });
        }

        [MenuItem("Squishy/Setup/Add Tilt-Shift Pass")]
        public static void AddTiltShiftPass()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(TiltShiftMaterialPath);
            if (rendererData == null || material == null) { Debug.LogWarning("Squishy: renderer or tilt-shift material missing."); return; }
            foreach (var f in rendererData.rendererFeatures)
                if (f is FullScreenPassRendererFeature fs && fs.passMaterial == material) return;

            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "TiltShiftGrade";
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            // Add through the serialized lists so URP's feature map stays in sync with the list.
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

        /// <summary>Generated scene: rebuilding overwrites it. Once we start hand-editing the scene, stop calling this.</summary>
        [MenuItem("Squishy/Setup/Rebuild Main Scene")]
        public static void CreateMainScene()
        {
            ContentSeeder.EnsureFolder("Assets/_Project/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.56f, 0.5f);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.95f, 0.90f, 0.82f); // cream
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            var follow = cameraGo.AddComponent<FollowCamera>();
            var tilt = cameraGo.AddComponent<TiltShiftController>();
            Wire(tilt, "targetCamera", camera);

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            var game = new GameObject("Game");
            Wire(game.AddComponent<GameBootstrap>(), "content", AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentSeeder.DatabasePath));

            var home = new GameObject("Home");
            var roomGo = new GameObject("Room");
            roomGo.transform.SetParent(home.transform, false);
            var room = roomGo.AddComponent<SteamerRoom>();
            Wire(room, "sceneryMaterial", AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Scenery.mat"));

            var squishyGo = new GameObject("Squishy");
            squishyGo.transform.SetParent(home.transform, false);
            var body = squishyGo.AddComponent<SquishyBody>();
            squishyGo.AddComponent<SquishyBrain>();
            Wire(body, "bodyMaterial", AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Squishy.mat"));
            Wire(body, "faceMaterial", AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Face.mat"));

            var homeScene = home.AddComponent<HomeScene>();
            Wire(homeScene, "room", room);
            Wire(homeScene, "squishy", body);
            Wire(homeScene, "followCamera", follow);
            Wire(homeScene, "tiltShift", tilt);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void Mat(string path, string shaderName, System.Action<Material> setup)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning("Squishy: shader not found: " + shaderName); return; }
            var m = new Material(shader);
            setup(m);
            AssetDatabase.CreateAsset(m, path);
        }

        private static void Wire(Object component, string field, Object value)
        {
            var so = new SerializedObject(component);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
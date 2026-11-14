using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sol3.Cinematics;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sol3.Game.Editor
{
    /// <summary>
    /// Configuración del juego completo: crea la escena principal, la pone la primera en la build,
    /// fija la orientación horizontal y prepara las builds de Android y WebGL (GitHub Pages en docs/).
    /// </summary>
    public static class GameSetup
    {
        public const string ScenePath = "Assets/Game/Scenes/SOL3_Game.unity";
        const string CatalogPath = "Assets/Cinematics3D/Config/CinematicCatalog.asset";
        const string MaterialsFolder = "Assets/Game/Resources/Materials";

        [MenuItem("SOL-3/Juego/1. Configurar proyecto y crear escena del juego", priority = 0)]
        public static void SetupAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            CreateMaterials();
            CreateScene();
            ConfigureBuildScenes();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[SOL-3] Juego configurado. Abre " + ScenePath + " y pulsa Play.");
            EditorUtility.DisplayDialog("SOL-3: Base Aurora",
                "Escena del juego creada y configurada (horizontal, escenas en la build, materiales).\n\nPulsa Play para jugar.", "Aceptar");
        }

        static void CreateMaterials()
        {
            Directory.CreateDirectory(MaterialsFolder);
            Material(MaterialsFolder + "/SOL3_SpriteLit.mat", "Universal Render Pipeline/2D/Sprite-Lit-Default");
            Material(MaterialsFolder + "/SOL3_SpriteUnlit.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }

        static void Material(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[SOL-3] No se encontró el shader '{shaderName}'. ¿Está activo URP 2D?");
                return;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                AssetDatabase.CreateAsset(new Material(shader), path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
        }

        static void CreateScene()
        {
            // Primero la escena nueva: NewScene descarga los recursos cargados antes de llamarla.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.09f);
            camera.transform.position = new Vector3(20f, 13f, -10f);
            if (CinematicLayers.Exists)
                camera.cullingMask &= ~CinematicLayers.Mask;
            cameraGo.AddComponent<AudioListener>();
            var data = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            data.SetRenderer(0);
            data.renderPostProcessing = false;

            var managerGo = new GameObject("GameManager");
            var manager = managerGo.AddComponent<GameManager>();
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CatalogPath);
            if (catalog == null)
                Debug.LogWarning("[SOL-3] No se encontró el catálogo de cinemáticas; el juego funcionará sin ellas.");
            var so = new SerializedObject(manager);
            so.FindProperty("cinematicCatalog").objectReferenceValue = catalog;
            so.FindProperty("playCinematics").boolValue = catalog != null;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void ConfigureBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == ScenePath || string.IsNullOrEmpty(s.path))
                    continue;
                // Se conservan las escenas de cinemáticas; las escenas de prueba quedan desactivadas.
                bool cinematic = s.path.StartsWith("Assets/Cinematics3D/Scenes/") && Path.GetFileName(s.path).StartsWith("CIN_");
                scenes.Add(new EditorBuildSettingsScene(s.path, cinematic));
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Scene CIN_", new[] { "Assets/Cinematics3D" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith("CIN_") && scenes.All(x => x.path != path))
                    scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "SOL-3 Base Aurora";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = false;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true; // GitHub Pages no envía cabeceras de compresión
            PlayerSettings.WebGL.dataCaching = true;
        }

        static string[] EnabledScenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        [MenuItem("SOL-3/Juego/2. Compilar WebGL para GitHub Pages (docs)", priority = 20)]
        public static void BuildWebGL()
        {
            ConfigurePlayer();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            if (report.summary.result == BuildResult.Succeeded)
            {
                File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
                Debug.Log($"[SOL-3] WebGL listo en {output} ({report.summary.totalSize / (1024 * 1024)} MB). Publica la carpeta docs/ con GitHub Pages.");
            }
            else
            {
                Debug.LogError("[SOL-3] La build WebGL falló: " + report.summary.result);
            }
        }

        [MenuItem("SOL-3/Juego/3. Compilar Android (APK horizontal)", priority = 21)]
        public static void BuildAndroid()
        {
            ConfigurePlayer();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android", "SOL3_BaseAurora.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[SOL-3] APK listo: {output} ({report.summary.totalSize / (1024 * 1024)} MB).");
            else
                Debug.LogError("[SOL-3] La build de Android falló: " + report.summary.result);
        }

        [MenuItem("SOL-3/Juego/Abrir escena del juego", priority = 40)]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}

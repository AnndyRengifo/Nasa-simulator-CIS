using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Fase E — Banco de pruebas de integración: una escena que simula el juego 2D (cámara 2D,
    /// estado de partida que avanza con el tiempo) con el CinematicManager real y el puente
    /// SimpleCinematicGameBridge. Desde ella se piden las siete cinemáticas exactamente como lo
    /// hará el juego. También genera un APK de prueba del sistema completo.
    /// </summary>
    internal static class CinematicTestHarnessBuilder
    {
        public const string Folder = CinematicEditorPaths.Root + "/Tests";
        public const string ScenePath = Folder + "/SOL3_CinematicTestHarness.unity";
        const string Title = "SOL-3 · Cinemáticas · Fase E";
        const string ApkPath = "Builds/Android/SOL3_Cinematics_System.apk";

        public static void BuildInteractive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog(Title, "Sal del modo Play y espera a que Unity termine de compilar.", "Entendido");
                return;
            }

            var report = new CinematicReport();
            Build(report);
            report.Publish("Fase E · Banco de pruebas", "SOL3_Cinematics_PhaseE.txt");
            EditorUtility.DisplayDialog(Title,
                $"{report.Summary}\n\nSe abrió {Path.GetFileName(ScenePath)}. Pulsa Play y usa los botones para pedir cada cinemática " +
                "como lo haría el juego 2D (el estado simulado debe quedar congelado durante la cinemática y continuar después).",
                "Cerrar");
        }

        public static bool Build(CinematicReport report)
        {
            const string category = "Banco de pruebas";
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            if (catalog == null || !CinematicLayers.Exists)
            {
                report.Error(category, "Falta la configuración de la Fase A (catálogo o capa).");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.Warning(category, "Cancelado: hay escenas con cambios sin guardar.");
                return false;
            }

            CinematicProjectSetup.EnsureFolder(Folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);

            // Cámara 2D del "juego": excluye la capa Cinematic3D, como debe hacer la cámara del juego real.
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CinematicBuildKit.Hex("#18283A");
            camera.cullingMask = ~CinematicLayers.Mask;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            var system = new GameObject("CinematicSystem");
            var manager = system.AddComponent<CinematicManager>();
            var bridge = system.AddComponent<SimpleCinematicGameBridge>();
            var harness = system.AddComponent<CinematicTestHarness>();

            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("catalog").objectReferenceValue = catalog;
            serializedManager.FindProperty("gameBridge").objectReferenceValue = bridge;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            var serializedHarness = new SerializedObject(harness);
            serializedHarness.FindProperty("manager").objectReferenceValue = manager;
            serializedHarness.FindProperty("bridge").objectReferenceValue = bridge;
            serializedHarness.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                report.Error(category, $"No se pudo guardar {ScenePath}.");
                return false;
            }

            report.Ok(category, $"Creada {ScenePath}: cámara 2D, CinematicManager con el catálogo, SimpleCinematicGameBridge y botones de prueba.");
            int missing = 0;
            foreach (var definition in catalog.Definitions)
            {
                if (definition == null)
                    continue;
                if (!definition.Scene.IsAssigned || AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.Scene.ScenePath) == null)
                    missing++;
                else if (!CinematicValidator.IsInBuildSettings(definition.Scene.ScenePath))
                    SceneReferenceDrawer.AddToBuild(definition.Scene.ScenePath);
            }
            if (missing > 0)
                report.Warning(category, $"{missing} cinemática(s) aún sin escena: genera las Fases C y D (si no, esas peticiones terminarán como 'Failed', lo que también sirve para probar los errores).");
            else
                report.Ok(category, "Las siete cinemáticas tienen escena y están en la build.");
            return true;
        }

        /// <summary>APK de desarrollo con el banco de pruebas como primera escena y las siete cinemáticas.</summary>
        public static void AndroidSystemBuild()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog(Title, "Falta el módulo Android Build Support.", "Cerrar");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog(Title, "Crea antes el banco de pruebas (SOL-3 > Cinemáticas > Fase E · Crear banco de pruebas).", "Cerrar");
                return;
            }

            var scenes = new List<string> { ScenePath };
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            foreach (var definition in catalog.Definitions)
            {
                if (definition != null && definition.Scene.IsAssigned && AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.Scene.ScenePath) != null)
                    scenes.Add(definition.Scene.ScenePath);
            }

            bool switchPlatform = EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android;
            if (!EditorUtility.DisplayDialog(Title,
                    $"Se generará un APK de DESARROLLO con el banco de pruebas y {scenes.Count - 1} cinemática(s).\n\n" +
                    (switchPlatform ? "La plataforma activa cambiará a Android (reimportación de varios minutos).\n\n" : string.Empty) +
                    "Requiere un Package Name de Android válido (Player Settings).\n\n" +
                    $"Salida: {ApkPath}", "Generar APK", "Cancelar"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (switchPlatform && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog(Title, "No se pudo cambiar la plataforma activa a Android.", "Cerrar");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath) ?? "Builds");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            });
            var summary = result.summary;
            string text = $"Resultado: {summary.result}\nErrores: {summary.totalErrors} · Avisos: {summary.totalWarnings}\n" +
                          $"Tamaño: {summary.totalSize / (1024f * 1024f):0.0} MB\nSalida: {ApkPath}\n\nInstala con: adb install -r \"{ApkPath}\"";
            if (summary.result == BuildResult.Succeeded)
                Debug.Log("[Cinematics] APK de prueba del sistema generado.\n" + text);
            else
                Debug.LogError("[Cinematics] Falló el APK de prueba del sistema.\n" + text);
            EditorUtility.DisplayDialog(Title, text, "Cerrar");
        }
    }
}

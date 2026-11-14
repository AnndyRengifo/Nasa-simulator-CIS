using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Genera un APK de desarrollo que SOLO contiene la escena de plantilla 3D, para comprobar en un
    /// dispositivo real que URP (renderer 3D), Timeline y Cinemachine funcionan en Android.
    /// No modifica la lista de escenas del juego.
    /// </summary>
    internal static class CinematicAndroidSmokeBuild
    {
        const string Title = "SOL-3 · Build de prueba Android";
        const string OutputPath = "Builds/Android/SOL3_Cinematics_Smoke.apk";

        public static void Run()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog(Title, "Falta el módulo Android Build Support (Unity Hub > Installs > Add modules).", "Cerrar");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CinematicEditorPaths.TemplateScene) == null)
            {
                EditorUtility.DisplayDialog(Title, "No existe la escena de plantilla. Ejecuta antes 'Fase A · Configurar proyecto'.", "Cerrar");
                return;
            }

            bool switchPlatform = EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android;
            string message =
                "Se generará un APK de DESARROLLO que solo contiene CIN_Template (10 s de cinemática de prueba).\n\n" +
                (switchPlatform
                    ? "La plataforma activa cambiará a Android: Unity reimportará los assets y puede tardar varios minutos.\n\n"
                    : string.Empty) +
                $"Salida: {OutputPath}\n\nInstálalo con: adb install -r \"{OutputPath}\"";

            if (!EditorUtility.DisplayDialog(Title, message, "Generar APK", "Cancelar"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (switchPlatform && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog(Title, "No se pudo cambiar la plataforma activa a Android.", "Cerrar");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? "Builds");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { CinematicEditorPaths.TemplateScene },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };

            BuildReport buildReport = BuildPipeline.BuildPlayer(options);
            var summary = buildReport.summary;
            string result =
                $"Resultado: {summary.result}\nErrores: {summary.totalErrors} · Avisos: {summary.totalWarnings}\n" +
                $"Tamaño: {summary.totalSize / (1024f * 1024f):0.0} MB · Tiempo: {summary.totalTime:mm\\:ss}\nSalida: {OutputPath}";

            if (summary.result == BuildResult.Succeeded)
                Debug.Log("[Cinematics] Build de prueba Android generada.\n" + result);
            else
                Debug.LogError("[Cinematics] La build de prueba Android falló.\n" + result);

            EditorUtility.DisplayDialog(Title, result, "Cerrar");
        }
    }
}

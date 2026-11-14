using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Comprueba que el sistema de cinemáticas está bien configurado: paquetes, render, capa,
    /// catálogo, definiciones, plantilla y ajustes de Android. Lo usan el menú, el asistente y
    /// (en la Fase F) las pruebas automáticas. Los elementos "PENDIENTE" son trabajo de fases
    /// posteriores, no errores.
    /// </summary>
    internal static class CinematicValidator
    {
        static readonly string[,] RequiredPackages =
        {
            { "com.unity.timeline", "Timeline" },
            { "com.unity.cinemachine", "Cinemachine" },
            { "com.unity.splines", "Splines (dependencia de Cinemachine)" },
            { "com.unity.render-pipelines.universal", "Universal Render Pipeline" },
            { "com.unity.ugui", "uGUI + TextMesh Pro" },
            { "com.unity.inputsystem", "Input System" },
        };

        static readonly Regex AndroidPackageName = new Regex(@"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$");

        public static CinematicReport Validate()
        {
            var report = new CinematicReport();
            report.Info("Unity", $"Editor {Application.unityVersion}. El juego principal sigue siendo 2D: las cinemáticas viven en escenas 3D independientes.");

            CheckPackages(report);
            CheckTextMeshPro(report);
            CheckLayer(report);

            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            CheckRendering(report, catalog);
            CheckCatalog(report, catalog);
            CheckTemplate(report);
            CheckPhaseB(report);
            CheckAndroid(report);
            return report;
        }

        static void CheckPackages(CinematicReport report)
        {
            for (int i = 0; i < RequiredPackages.GetLength(0); i++)
            {
                string name = RequiredPackages[i, 0];
                string label = RequiredPackages[i, 1];
                var info = UnityEditor.PackageManager.PackageInfo.FindForPackageName(name);
                if (info == null)
                    report.Error("Paquetes", $"{label} ({name}) no está instalado.");
                else
                    report.Ok("Paquetes", $"{label} {info.version} ({info.source}).");
            }
        }

        static void CheckTextMeshPro(CinematicReport report)
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
                report.Ok("TextMesh Pro", "Recursos esenciales importados.");
            else
                report.Warning("TextMesh Pro", "Faltan los recursos esenciales (se importan con la configuración de la Fase A).");
        }

        static void CheckLayer(CinematicReport report)
        {
            int layer = CinematicLayers.Index;
            if (layer >= 0)
                report.Ok("Capa", $"'{CinematicLayers.LayerName}' existe (índice {layer}). La cámara 2D del juego debe excluirla de su Culling Mask.");
            else
                report.Error("Capa", $"No existe la capa '{CinematicLayers.LayerName}'. Ejecuta la configuración de la Fase A.");
        }

        static void CheckRendering(CinematicReport report, CinematicCatalog catalog)
        {
            const string category = "Render";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CinematicEditorPaths.RendererAsset);
            if (rendererData == null)
            {
                report.Error(category, $"No existe {CinematicEditorPaths.RendererAsset}.");
                return;
            }

            if (rendererData.postProcessData == null)
                report.Warning(category, "El renderer 3D no tiene PostProcessData: el post-procesado no se verá.");

            var urpAssets = CinematicProjectSetup.CollectUrpAssets();
            if (urpAssets.Count == 0)
            {
                report.Error(category, "Ningún nivel de calidad usa URP.");
                return;
            }

            int index = catalog != null ? catalog.Globals.cinematicRendererIndex : -1;
            foreach (var urp in urpAssets)
            {
                var list = urp.rendererDataList;
                if (index < 0 || index >= list.Length || list[index] != rendererData)
                    report.Error(category, $"'{urp.name}': el índice {index} del catálogo no apunta al renderer 3D. Ejecuta la configuración de la Fase A.");
                else
                    report.Ok(category, $"'{urp.name}': renderer 3D en el índice {index}; por defecto {CinematicProjectSetup.DefaultRendererLabel(urp)} (juego 2D intacto).");

                report.Info(category,
                    $"'{urp.name}': HDR {(urp.supportsHDR ? "sí" : "no")}, MSAA x{urp.msaaSampleCount}, " +
                    $"sombra principal {(urp.supportsMainLightShadows ? urp.mainLightShadowmapResolution + " px" : "desactivada")}, " +
                    $"distancia de sombras {urp.shadowDistance:0} m, luces adicionales por objeto {urp.maxAdditionalLightsCount}.");
            }
        }

        static void CheckCatalog(CinematicReport report, CinematicCatalog catalog)
        {
            const string category = "Catálogo";
            if (catalog == null)
            {
                report.Error(category, $"No existe {CinematicEditorPaths.Catalog}. Ejecuta la configuración de la Fase A.");
                return;
            }

            report.Ok(category, $"Catálogo central: {CinematicEditorPaths.Catalog} ({catalog.Definitions.Count} definiciones).");

            var seen = new HashSet<string>();
            foreach (var definition in catalog.Definitions)
            {
                if (definition == null)
                {
                    report.Error(category, "Hay una entrada vacía en la lista de definiciones.");
                    continue;
                }
                if (!seen.Add(definition.Id))
                    report.Error(category, $"Identificador duplicado: '{definition.Id}'.");
                if (!CinematicIds.IsKnown(definition.Id))
                    report.Warning(category, $"'{definition.Id}' ({definition.name}) no es uno de los siete identificadores oficiales.");
            }

            foreach (var id in CinematicIds.All)
            {
                if (!seen.Contains(id))
                    report.Error(category, $"Falta la definición de '{id}'.");
            }

            foreach (var definition in catalog.Definitions)
            {
                if (definition != null)
                    CheckDefinition(report, definition);
            }
        }

        static void CheckDefinition(CinematicReport report, CinematicDefinition definition)
        {
            string category = $"Cinemática '{definition.Id}'";

            if (!definition.Scene.IsAssigned)
            {
                report.Pending(category, "Escena sin asignar (se crea en la Fase C/D).");
            }
            else if (AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.Scene.ScenePath) == null)
            {
                report.Error(category, $"La escena '{definition.Scene.ScenePath}' no existe.");
            }
            else if (!IsInBuildSettings(definition.Scene.ScenePath))
            {
                report.Error(category, $"La escena '{definition.Scene.SceneName}' no está en la lista de escenas de la build.");
            }
            else
            {
                report.Ok(category, $"Escena '{definition.Scene.SceneName}' incluida en la build.");
            }

            if (definition.TimelineOverride != null)
                report.Info(category, $"Usa el Timeline de sustitución '{definition.TimelineOverride.name}'.");

            if (definition.ExpectedDuration <= 0f)
                report.Warning(category, "Duración prevista sin definir.");

            if (definition.SubtitleSheets.Count == 0)
            {
                report.Pending(category, "Sin subtítulos todavía (se crean con la cinemática).");
            }
            else
            {
                foreach (var sheet in definition.SubtitleSheets)
                {
                    if (sheet == null)
                    {
                        report.Error(category, "Hay una hoja de subtítulos vacía en la lista.");
                        continue;
                    }

                    var problems = new List<string>();
                    if (!sheet.Validate(problems))
                    {
                        foreach (var problem in problems)
                            report.Error(category, problem);
                    }

                    if (sheet.CinematicId != definition.Id)
                        report.Warning(category, $"La hoja '{sheet.name}' indica la cinemática '{sheet.CinematicId}'.");
                }
            }

            if (definition.EndBehaviour == CinematicEndBehaviour.LoadScene)
            {
                if (!definition.ReturnScene.IsAssigned)
                    report.Error(category, "'End Behaviour' es LoadScene pero no hay 'Return Scene'.");
                else if (!IsInBuildSettings(definition.ReturnScene.ScenePath))
                    report.Error(category, $"La escena de retorno '{definition.ReturnScene.SceneName}' no está en la build.");
            }
        }

        static void CheckTemplate(CinematicReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CinematicEditorPaths.TemplateScene) != null)
                report.Ok("Plantilla", $"{CinematicEditorPaths.TemplateScene} existe.");
            else
                report.Error("Plantilla", $"No existe {CinematicEditorPaths.TemplateScene}.");

            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(CinematicEditorPaths.TemplateTimeline) != null)
                report.Ok("Plantilla", $"{CinematicEditorPaths.TemplateTimeline} existe.");
            else
                report.Error("Plantilla", $"No existe {CinematicEditorPaths.TemplateTimeline}.");
        }

        static void CheckPhaseB(CinematicReport report)
        {
            const string category = "Recursos (Fase B)";
            int missing = 0;
            foreach (var path in CinematicPhaseB.ExpectedPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    missing++;
            }

            if (missing == CinematicPhaseB.ExpectedPrefabs.Length)
                report.Pending(category, "Recursos aún no generados (SOL-3 > Cinemáticas > Fase B · Crear recursos y galería).");
            else if (missing > 0)
                report.Error(category, $"Faltan {missing} de {CinematicPhaseB.ExpectedPrefabs.Length} prefabs. Ejecuta de nuevo la Fase B.");
            else
                report.Ok(category, $"{CinematicPhaseB.ExpectedPrefabs.Length} prefabs principales presentes (personajes, escenarios, espacio y efectos).");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PhaseBPaths.GalleryScene) != null)
                report.Ok(category, $"Galería: {PhaseBPaths.GalleryScene}.");
        }

        static void CheckAndroid(CinematicReport report)
        {
            const string category = "Android";

            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                report.Ok(category, "Módulo Android Build Support instalado.");
            else
                report.Error(category, "Falta el módulo Android Build Support (instálalo desde Unity Hub).");

            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            if (backend == ScriptingImplementation.IL2CPP)
                report.Ok(category, "Scripting backend IL2CPP.");
            else
                report.Warning(category, $"Scripting backend {backend}: Google Play exige ARM64, que requiere IL2CPP.");

            var architectures = PlayerSettings.Android.targetArchitectures;
            if ((architectures & AndroidArchitecture.ARM64) != 0)
                report.Ok(category, $"Arquitecturas: {architectures}.");
            else
                report.Error(category, "ARM64 no está activado (obligatorio en Google Play).");

            var minSdk = PlayerSettings.Android.minSdkVersion;
            if ((int)minSdk >= 26)
                report.Ok(category, $"API mínima {(int)minSdk} (Android 8.0 o superior).");
            else
                report.Warning(category, $"API mínima {(int)minSdk}: se recomienda 26 o superior.");

            if (PlayerSettings.colorSpace == ColorSpace.Linear)
                report.Ok(category, "Espacio de color Linear.");
            else
                report.Warning(category, "Espacio de color Gamma: la iluminación 3D se verá distinta a la diseñada.");

            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
                report.Ok(category, "APIs gráficas automáticas (Vulkan con respaldo OpenGL ES 3).");
            else
                report.Info(category, "APIs gráficas: " + string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)));

            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (string.IsNullOrEmpty(identifier) || !AndroidPackageName.IsMatch(identifier))
                report.Warning(category, $"Package Name '{identifier}' no es válido para Android (solo letras, números y '_', cada segmento debe empezar por letra). " +
                                         "Defínelo en Player Settings > Android > Other Settings antes de generar un APK.");
            else
                report.Ok(category, $"Package Name '{identifier}'.");

            var orientation = PlayerSettings.defaultInterfaceOrientation;
            bool portraitAllowed = orientation == UIOrientation.Portrait || orientation == UIOrientation.PortraitUpsideDown ||
                                   (orientation == UIOrientation.AutoRotation &&
                                    (PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown));
            if (portraitAllowed)
                report.Warning(category, $"Orientación '{orientation}' permite vertical. Las cinemáticas se componen en horizontal; " +
                                         "se recomienda fijar Landscape (decisión del juego 2D).");
            else
                report.Ok(category, $"Orientación '{orientation}'.");

            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            if (activeTarget == BuildTarget.Android)
                report.Ok(category, "Plataforma activa: Android.");
            else
                report.Info(category, $"Plataforma activa: {activeTarget}. Para probar en un dispositivo cambia a Android en File > Build Profiles.");
        }

        internal static bool IsInBuildSettings(string scenePath)
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && scene.path == scenePath)
                    return true;
            }
            return false;
        }
    }
}

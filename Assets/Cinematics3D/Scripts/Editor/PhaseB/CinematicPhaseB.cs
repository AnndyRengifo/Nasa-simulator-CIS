using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Fase B — Recursos y personajes. Crea (o completa) texturas, materiales, mallas, personajes,
    /// animaciones, escenarios, props, efectos y la escena de galería. Si la Fase A no está
    /// configurada todavía, la ejecuta primero.
    /// </summary>
    internal static class CinematicPhaseB
    {
        const string Title = "SOL-3 · Cinemáticas · Fase B";

        /// <summary>Prefabs que la Fase B debe producir (los usa también el validador).</summary>
        internal static readonly string[] ExpectedPrefabs =
        {
            PhaseBPaths.Prefab(PhaseBPaths.Characters, "PF_Astronaut"),
            PhaseBPaths.Prefab(PhaseBPaths.Characters, "PF_AURA"),
            PhaseBPaths.Prefab(PhaseBPaths.Environment, "PF_Set_MarsSurface"),
            PhaseBPaths.Prefab(PhaseBPaths.Environment, "PF_BaseAurora_Exterior"),
            PhaseBPaths.Prefab(PhaseBPaths.Environment, "PF_Room_ControlCenter"),
            PhaseBPaths.Prefab(PhaseBPaths.Environment, "PF_Room_PowerRoom"),
            PhaseBPaths.Prefab(PhaseBPaths.Environment, "PF_Room_Shelter"),
            PhaseBPaths.Prefab(PhaseBPaths.Environment, "PF_Room_EarthControl"),
            PhaseBPaths.Prefab(PhaseBPaths.Environment, "PF_Space_Mars"),
            PhaseBPaths.Prefab(PhaseBPaths.Environment, "PF_Space_Earth"),
            PhaseBPaths.Prefab(PhaseBPaths.Environment, "PF_Space_Sun"),
            PhaseBPaths.Prefab(PhaseBPaths.Fx, "PF_FX_MarsDust"),
            PhaseBPaths.Prefab(PhaseBPaths.Fx, "PF_FX_SolarFlare"),
            PhaseBPaths.Prefab(PhaseBPaths.Fx, "PF_FX_Sparks"),
            PhaseBPaths.Prefab(PhaseBPaths.Fx, "PF_FX_TransmissionPulse"),
        };

        public static void RunInteractive(bool overwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog(Title, "Sal del modo Play y espera a que Unity termine de compilar o importar.", "Entendido");
                return;
            }

            string message = overwrite
                ? "Se REGENERARÁN todos los recursos provisionales de la Fase B (texturas, materiales, mallas, animaciones y prefabs), " +
                  "sobrescribiendo los cambios manuales hechos en ellos. Las referencias (GUID) se conservan.\n\n¿Continuar?"
                : "Se crearán los recursos de la Fase B que falten:\n\n" +
                  "• Astronauta y AURA (prefabs + 12 animaciones)\n" +
                  "• Marte, Base Aurora (exterior), sala de control, sala de máquinas, refugio y Control Tierra\n" +
                  "• Marte, la Tierra y el Sol vistos desde el espacio\n" +
                  "• Props, efectos de partículas, materiales, texturas y cielos\n" +
                  "• Escena de prueba CIN_FaseB_Galeria (se abrirá)\n\n" +
                  "Si la Fase A aún no se configuró, se configura primero. Puede tardar 1-2 minutos.";

            if (!EditorUtility.DisplayDialog(Title, message, overwrite ? "Regenerar" : "Crear", "Cancelar"))
                return;

            var report = Run(overwrite);
            string log = report.Publish(overwrite ? "Fase B (regeneración)" : "Fase B", "SOL3_Cinematics_PhaseB.txt");
            var validation = CinematicValidator.Validate();
            string validationLog = validation.Publish("Validación del sistema de cinemáticas", "SOL3_Cinematics_Validation.txt");

            EditorUtility.DisplayDialog(Title,
                $"Fase B: {report.Summary}\nValidación: {validation.Summary}\n\n" +
                "Pulsa Play en la escena abierta (CIN_FaseB_Galeria) para ver el recorrido.\n\n" +
                $"Informes en la Consola y en:\n{log}\n{validationLog}", "Cerrar");
        }

        public static CinematicReport Run(bool overwrite)
        {
            var report = new CinematicReport();

            if (!IsPhaseAReady())
            {
                report.Info("Fase A", "La Fase A no estaba configurada: se ejecuta su asistente primero.");
                var phaseA = CinematicProjectSetup.Run();
                foreach (var entry in phaseA.Entries)
                    report.Add(entry.Severity, "Fase A · " + entry.Category, entry.Message);
                if (!IsPhaseAReady())
                {
                    report.Error("Fase B", "La Fase A no quedó configurada (capa, renderer o catálogo). Revisa los errores anteriores.");
                    return report;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.Warning("Fase B", "Cancelado: hay escenas con cambios sin guardar.");
                return report;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            int rendererIndex = catalog.Globals.cinematicRendererIndex;
            var ctx = new BuildContext { Overwrite = overwrite, Layer = CinematicLayers.Index, Report = report };

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                foreach (var folder in PhaseBPaths.Folders)
                    CinematicProjectSetup.EnsureFolder(folder);

                Step("Texturas procedurales", 0.05f, () => CinematicTextureFactory.BuildAll(ctx), report);
                Step("Materiales y cielos", 0.25f, () => CinematicMaterialLibrary.BuildAll(ctx), report);
                Step("Escenarios, props y efectos", 0.40f, () => CinematicEnvironmentBuilder.BuildAll(ctx), report);
                Step("Astronauta y AURA", 0.70f, () => CinematicCharacterBuilder.BuildAll(ctx), report);
                AssetDatabase.SaveAssets();
                Step("Escena de galería", 0.85f, () => CinematicGalleryBuilder.Build(ctx, rendererIndex), report);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            report.Info("Recursos", $"Creados: {ctx.Created} · actualizados: {ctx.Updated} · reutilizados: {ctx.Reused}.");
            report.Info("Recursos", "Todos los modelos son PROVISIONALES (primitivas y mallas procedurales), preparados para sustituirse sin tocar las cinemáticas.");
            foreach (var path in ExpectedPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    report.Error("Recursos", $"No se generó {path}.");
            }
            return report;
        }

        static void Step(string label, float progress, Action action, CinematicReport report)
        {
            EditorUtility.DisplayProgressBar(Title, label + "…", progress);
            try
            {
                action();
                report.Ok("Fase B", label + ": listo.");
            }
            catch (Exception exception)
            {
                report.Error("Fase B", $"{label}: {exception.Message}");
                Debug.LogException(exception);
            }
        }

        internal static bool IsPhaseAReady()
        {
            if (!CinematicLayers.Exists)
                return false;
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CinematicEditorPaths.RendererAsset);
            if (catalog == null || renderer == null)
                return false;
            int index = catalog.Globals.cinematicRendererIndex;
            foreach (var urp in CinematicProjectSetup.CollectUrpAssets())
            {
                var list = urp.rendererDataList;
                if (index < 0 || index >= list.Length || list[index] != renderer)
                    return false;
            }
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(CinematicEditorPaths.DefaultVolumeProfile) != null;
        }
    }
}

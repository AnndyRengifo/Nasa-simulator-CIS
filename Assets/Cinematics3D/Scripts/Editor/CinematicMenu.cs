using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>Menú "SOL-3 > Cinemáticas" del Editor.</summary>
    internal static class CinematicMenu
    {
        const string Root = "SOL-3/Cinemáticas/";
        const string Title = "SOL-3 · Cinemáticas";

        [MenuItem(Root + "Fase A · Configurar proyecto", priority = 0)]
        static void Setup() => CinematicProjectSetup.RunInteractive();

        [MenuItem(Root + "Fase B · Crear recursos y galería", priority = 1)]
        static void PhaseB() => CinematicPhaseB.RunInteractive(false);

        [MenuItem(Root + "Fase B · Regenerar recursos (sobrescribe)", priority = 2)]
        static void PhaseBRegenerate() => CinematicPhaseB.RunInteractive(true);

        [MenuItem(Root + "Fase C · Generar cinemáticas narrativas", priority = 3)]
        static void PhaseC() => CinematicPhaseC.RunInteractive();

        [MenuItem(Root + "Fase D · Generar finales", priority = 4)]
        static void PhaseD() => CinematicPhaseD.RunInteractive();

        [MenuItem(Root + "Fase E · Crear banco de pruebas", priority = 5)]
        static void PhaseE() => CinematicTestHarnessBuilder.BuildInteractive();

        [MenuItem(Root + "Fase F · Optimizar y medir rendimiento", priority = 6)]
        static void PhaseFOptimize() => CinematicPhaseF.OptimizeAndMeasure();

        [MenuItem(Root + "Fase F · Ejecutar pruebas automáticas", priority = 7)]
        static void PhaseFTests() => CinematicPhaseF.RunTests();

        [MenuItem(Root + "Abrir banco de pruebas (Fase E)", priority = 43)]
        static void OpenHarness()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CinematicTestHarnessBuilder.ScenePath) == null)
            {
                EditorUtility.DisplayDialog(Title, "El banco de pruebas no existe todavía. Ejecuta 'Fase E · Crear banco de pruebas'.", "Cerrar");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(CinematicTestHarnessBuilder.ScenePath, OpenSceneMode.Single);
        }

        [MenuItem(Root + "Android · Build de prueba del sistema completo", priority = 101)]
        static void AndroidSystemBuild() => CinematicTestHarnessBuilder.AndroidSystemBuild();

        [MenuItem(Root + "Abrir galería de la Fase B", priority = 42)]
        static void OpenGallery()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PhaseBPaths.GalleryScene) == null)
            {
                EditorUtility.DisplayDialog(Title, "La galería no existe todavía. Ejecuta 'Fase B · Crear recursos y galería'.", "Cerrar");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(PhaseBPaths.GalleryScene, OpenSceneMode.Single);
        }

        [MenuItem(Root + "Validar sistema", priority = 20)]
        static void Validate()
        {
            var report = CinematicValidator.Validate();
            string path = report.Publish("Validación del sistema de cinemáticas", "SOL3_Cinematics_Validation.txt");
            EditorUtility.DisplayDialog(Title, $"{report.Summary}\n\nDetalle en la Consola y en {path}.", "Cerrar");
        }

        [MenuItem(Root + "Abrir escena de plantilla", priority = 40)]
        static void OpenTemplate()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CinematicEditorPaths.TemplateScene) == null)
            {
                EditorUtility.DisplayDialog(Title, "La plantilla no existe todavía. Ejecuta 'Fase A · Configurar proyecto'.", "Cerrar");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(CinematicEditorPaths.TemplateScene, OpenSceneMode.Single);
        }

        [MenuItem(Root + "Reconstruir escena de plantilla", priority = 41)]
        static void RebuildTemplate()
        {
            if (!EditorUtility.DisplayDialog(Title,
                    "Se regenerarán CIN_Template, TL_Template y sus animaciones (los cambios manuales en ellos se perderán). ¿Continuar?",
                    "Reconstruir", "Cancelar"))
                return;

            var report = new CinematicReport();
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            int rendererIndex = catalog != null ? catalog.Globals.cinematicRendererIndex : -1;
            CinematicTemplateBuilder.Build(CinematicLayers.Index, rendererIndex, true, report);
            report.Publish("Reconstrucción de la plantilla", "SOL3_Cinematics_Template.txt");
        }

        [MenuItem(Root + "Asignar capa Cinematic3D a las escenas abiertas", priority = 60)]
        static void AssignLayer()
        {
            if (!CinematicLayers.Exists)
            {
                EditorUtility.DisplayDialog(Title, "La capa Cinematic3D no existe. Ejecuta 'Fase A · Configurar proyecto'.", "Cerrar");
                return;
            }

            int total = 0;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!CinematicLayerAutoAssign.IsCinematicScenePath(scene.path))
                    continue;
                int changed = CinematicLayers.AssignToScene(scene);
                if (changed > 0)
                    EditorSceneManager.MarkSceneDirty(scene);
                total += changed;
            }
            EditorUtility.DisplayDialog(Title, $"Capa asignada a {total} objeto(s) de las escenas cinemáticas abiertas.", "Cerrar");
        }

        [MenuItem(Root + "Seleccionar catálogo central", priority = 80)]
        static void SelectCatalog() => SelectAsset(CinematicEditorPaths.Catalog);

        [MenuItem(Root + "Abrir documentación", priority = 81)]
        static void OpenDocumentation() => SelectAsset(CinematicEditorPaths.Readme);

        [MenuItem(Root + "Android · Build de prueba de la plantilla", priority = 100)]
        static void AndroidSmokeBuild() => CinematicAndroidSmokeBuild.Run();

        static void SelectAsset(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                EditorUtility.DisplayDialog(Title, $"No existe {path}. Ejecuta 'Fase A · Configurar proyecto'.", "Cerrar");
                return;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}

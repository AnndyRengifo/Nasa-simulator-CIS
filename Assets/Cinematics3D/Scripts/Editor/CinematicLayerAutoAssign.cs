using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Al guardar cualquier escena de Assets/Cinematics3D/Scenes, asigna automáticamente la capa
    /// Cinematic3D a todos sus objetos. Así los objetos nuevos (primitivas, modelos importados...)
    /// siempre son visibles para la cámara cinemática y nunca para la cámara 2D del juego.
    /// </summary>
    [InitializeOnLoad]
    internal static class CinematicLayerAutoAssign
    {
        static CinematicLayerAutoAssign()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        static void OnSceneSaving(Scene scene, string path)
        {
            if (!IsCinematicScenePath(path) || !CinematicLayers.Exists)
                return;

            int changed = CinematicLayers.AssignToScene(scene);
            if (changed > 0)
                Debug.Log($"[Cinematics] Capa '{CinematicLayers.LayerName}' asignada a {changed} objeto(s) de '{scene.name}' al guardar.");
        }

        internal static bool IsCinematicScenePath(string path) =>
            !string.IsNullOrEmpty(path) &&
            path.Replace('\\', '/').StartsWith(CinematicEditorPaths.Scenes + "/", StringComparison.OrdinalIgnoreCase);
    }
}

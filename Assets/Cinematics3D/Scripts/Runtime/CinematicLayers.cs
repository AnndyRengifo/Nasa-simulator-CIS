using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Aislamiento visual entre el juego 2D y las cinemáticas 3D.
    /// Todo lo que pertenece a una cinemática vive en la capa "Cinematic3D"; la cámara cinemática
    /// solo renderiza esa capa, de modo que los sprites del juego 2D (que siguen cargados en memoria
    /// mientras se reproduce la cinemática) nunca aparecen en los planos 3D, y viceversa.
    /// La capa se busca siempre por nombre: nunca se usa un índice fijo.
    /// </summary>
    public static class CinematicLayers
    {
        /// <summary>Nombre de la capa reservada para las cinemáticas 3D.</summary>
        public const string LayerName = "Cinematic3D";

        /// <summary>Índice de la capa, o -1 si todavía no existe (ejecuta la configuración de la Fase A).</summary>
        public static int Index => LayerMask.NameToLayer(LayerName);

        /// <summary>Máscara de bits de la capa, o 0 si no existe.</summary>
        public static int Mask
        {
            get
            {
                int index = Index;
                return index >= 0 ? 1 << index : 0;
            }
        }

        /// <summary>True si la capa existe en el proyecto.</summary>
        public static bool Exists => Index >= 0;

        /// <summary>Asigna la capa a un objeto y a toda su jerarquía. Devuelve cuántos objetos cambiaron.</summary>
        public static int AssignRecursively(GameObject root, int layer)
        {
            if (root == null || layer < 0 || layer > 31)
                return 0;

            int changed = 0;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var go = transforms[i].gameObject;
                if (go.layer == layer)
                    continue;
                go.layer = layer;
                changed++;
            }
            return changed;
        }

        /// <summary>Asigna la capa Cinematic3D a todos los objetos de una escena cargada.</summary>
        public static int AssignToScene(Scene scene)
        {
            int layer = Index;
            if (layer < 0 || !scene.IsValid() || !scene.isLoaded)
                return 0;

            int changed = 0;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                changed += AssignRecursively(roots[i], layer);
            return changed;
        }
    }
}

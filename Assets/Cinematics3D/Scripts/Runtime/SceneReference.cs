using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Referencia a una escena utilizable en tiempo de ejecución.
    /// Guarda la ruta (lo que usa el juego para cargarla) y el GUID (para que el Editor la siga
    /// encontrando aunque la escena se mueva o se renombre). En el Inspector se muestra como un
    /// selector de escenas con aviso si la escena no está incluida en la build.
    /// </summary>
    [Serializable]
    public sealed class SceneReference
    {
        [SerializeField] string guid = string.Empty;
        [SerializeField] string path = string.Empty;

        public SceneReference()
        {
        }

        public SceneReference(string scenePath, string sceneGuid)
        {
            path = scenePath ?? string.Empty;
            guid = sceneGuid ?? string.Empty;
        }

        /// <summary>GUID del asset de escena (solo informativo en tiempo de ejecución).</summary>
        public string AssetGuid => guid;

        /// <summary>Ruta de la escena, por ejemplo "Assets/Cinematics3D/Scenes/CIN_intro.unity".</summary>
        public string ScenePath => path;

        /// <summary>True si hay una escena asignada.</summary>
        public bool IsAssigned => !string.IsNullOrEmpty(path);

        /// <summary>Nombre de la escena sin extensión.</summary>
        public string SceneName => IsAssigned ? System.IO.Path.GetFileNameWithoutExtension(path) : string.Empty;

        /// <summary>Índice en la lista de escenas de la build, o -1 si no está incluida.</summary>
        public int BuildIndex => IsAssigned ? SceneUtility.GetBuildIndexByScenePath(path) : -1;

        /// <summary>True si la escena está incluida en la build y por tanto se puede cargar.</summary>
        public bool IsInBuild => BuildIndex >= 0;

        public override string ToString() => IsAssigned ? path : "(sin asignar)";
    }
}

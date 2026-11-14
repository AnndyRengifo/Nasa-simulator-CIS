using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol3.Cinematics
{
    /// <summary>
    /// CONFIGURACIÓN CENTRAL de las cinemáticas: relaciona cada identificador estable con su
    /// definición (escena, Timeline, subtítulos, omisión, final...) y guarda los ajustes globales.
    /// El sistema de reproducción solo conoce este catálogo: para reasignar una cinemática no hay
    /// que tocar código.
    /// </summary>
    [CreateAssetMenu(fileName = "CinematicCatalog", menuName = "SOL-3/Cinemáticas/Catálogo central", order = 0)]
    public sealed class CinematicCatalog : ScriptableObject
    {
        [SerializeField] CinematicGlobalSettings globals = new CinematicGlobalSettings();

        [Tooltip("Una definición por cinemática. El orden no importa; el identificador sí.")]
        [SerializeField] List<CinematicDefinition> definitions = new List<CinematicDefinition>();

        public CinematicGlobalSettings Globals => globals;
        public IReadOnlyList<CinematicDefinition> Definitions => definitions;

        /// <summary>Busca la definición de un identificador. Devuelve false si no existe.</summary>
        public bool TryGet(string cinematicId, out CinematicDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(cinematicId))
                return false;

            for (int i = 0; i < definitions.Count; i++)
            {
                var candidate = definitions[i];
                if (candidate != null && string.Equals(candidate.Id, cinematicId, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Ajustes comunes a todas las cinemáticas.</summary>
    [Serializable]
    public sealed class CinematicGlobalSettings
    {
        [Header("Render")]
        [Tooltip("Índice del Universal Renderer 3D dentro de la lista de renderers del URP Asset. " +
                 "Lo escribe el asistente de configuración; el renderer por defecto (0) sigue siendo el 2D del juego.")]
        public int cinematicRendererIndex = -1;

        [Tooltip("Relación de aspecto de composición (ancho/alto). Las bandas negras se ajustan al dispositivo.")]
        [Min(1f)] public float targetAspect = 2f;

        [Header("Idioma")]
        [Tooltip("Idioma de subtítulos por defecto (ISO 639-1).")]
        public string defaultLanguage = "es";

        [Tooltip("Idioma usado si una cinemática no tiene hoja en el idioma activo.")]
        public string fallbackLanguage = "es";

        [Header("Seguridad")]
        [Tooltip("Segundos de margen sobre la duración prevista antes de forzar el final si el Timeline no terminara.")]
        [Min(0f)] public float watchdogMargin = 10f;

        [Tooltip("Tiempo máximo de carga de una escena cinemática antes de abortarla sin bloquear la partida.")]
        [Min(1f)] public float sceneLoadTimeout = 20f;

        [Header("Integración con el juego 2D")]
        [Tooltip("Pausar el juego 2D con Time.timeScale = 0 durante la cinemática. " +
                 "Las cinemáticas usan tiempo no escalado, así que no se ven afectadas.")]
        public bool pauseWithTimeScale = true;

        [Tooltip("Liberar recursos (Resources.UnloadUnusedAssets) después de descargar la escena 3D.")]
        public bool unloadUnusedAssetsAfterPlayback = true;

        [Tooltip("Desactivar durante la cinemática las cámaras y AudioListeners del juego 2D (y restaurarlos al terminar).")]
        public bool disableGameCameras = true;

        [Tooltip("Pausar el audio del juego 2D (AudioListener.pause) durante la cinemática. El audio de la cinemática sigue sonando.")]
        public bool pauseGameAudio = true;

        [Header("Audio")]
        [Tooltip("Opcional. AudioMixer con parámetros expuestos para el volumen de las cinemáticas (en dB).")]
        public UnityEngine.Audio.AudioMixer audioMixer;
        public string masterVolumeParameter = "CinematicMaster";
        public string musicVolumeParameter = "CinematicMusic";
        public string voiceVolumeParameter = "CinematicVoice";
        public string sfxVolumeParameter = "CinematicSFX";

        [Header("Interfaz")]
        [Tooltip("Texto del botón para omitir.")]
        public string skipLabel = "Omitir  »";
    }
}

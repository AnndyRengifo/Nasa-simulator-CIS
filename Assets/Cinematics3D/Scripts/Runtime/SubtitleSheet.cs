using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Hoja de subtítulos de UNA cinemática en UN idioma.
    /// Los diálogos están separados de las animaciones y de las cámaras: cambiar un texto, su
    /// tiempo o su audio de voz no requiere tocar el Timeline ni la escena.
    /// Los tiempos se expresan en segundos del Timeline de la cinemática (el presentador de
    /// subtítulos lee el tiempo del PlayableDirector, así que siempre van sincronizados).
    /// </summary>
    [CreateAssetMenu(fileName = "SUB_", menuName = "SOL-3/Cinemáticas/Hoja de subtítulos", order = 2)]
    public sealed class SubtitleSheet : ScriptableObject
    {
        [Tooltip("Cinemática a la que pertenece esta hoja.")]
        [CinematicId, SerializeField] string cinematicId = string.Empty;

        [Tooltip("Código de idioma ISO 639-1: es, en, pt...")]
        [SerializeField] string language = "es";

        [SerializeField] List<SubtitleLine> lines = new List<SubtitleLine>();

        public string CinematicId => cinematicId;
        public string Language => language;
        public IReadOnlyList<SubtitleLine> Lines => lines;

        /// <summary>Devuelve la línea visible en el instante indicado (segundos del Timeline) o null.</summary>
        public SubtitleLine GetLineAt(double time)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line != null && time >= line.startTime && time < line.endTime)
                    return line;
            }
            return null;
        }

        /// <summary>Comprueba tiempos y textos. Añade los problemas encontrados a la lista y devuelve true si no hay ninguno.</summary>
        public bool Validate(List<string> problems)
        {
            int before = problems.Count;
            if (string.IsNullOrEmpty(language))
                problems.Add($"{name}: falta el código de idioma.");

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                string label = $"{name} línea {i + 1}";
                if (line == null)
                {
                    problems.Add($"{label}: línea vacía.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line.text))
                    problems.Add($"{label}: texto vacío.");
                if (line.endTime <= line.startTime)
                    problems.Add($"{label}: el final ({line.endTime:0.00}s) debe ser mayor que el inicio ({line.startTime:0.00}s).");
                if (i > 0 && lines[i - 1] != null && line.startTime < lines[i - 1].endTime)
                    problems.Add($"{label}: se solapa con la línea anterior.");
            }
            return problems.Count == before;
        }
    }

    /// <summary>Una línea de diálogo con su personaje, su intervalo de tiempo y su voz opcional.</summary>
    [Serializable]
    public sealed class SubtitleLine
    {
        [Tooltip("Clave estable de la línea, por ejemplo 'intro_010'. Útil para localización y para sustituir audios.")]
        public string lineId = string.Empty;

        [Tooltip("Personaje que habla.")]
        public CinematicSpeaker speaker;

        [TextArea(2, 4)]
        public string text = string.Empty;

        [Tooltip("Inicio en segundos del Timeline.")]
        [Min(0f)] public float startTime;

        [Tooltip("Final en segundos del Timeline.")]
        [Min(0f)] public float endTime;

        [Tooltip("Audio de voz opcional. Se reproduce al empezar la línea por el grupo de mezcla de voces.")]
        public AudioClip voiceClip;

        [Range(0f, 1f)] public float voiceVolume = 1f;
    }
}

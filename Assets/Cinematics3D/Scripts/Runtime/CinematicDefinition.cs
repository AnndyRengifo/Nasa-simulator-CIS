using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace Sol3.Cinematics
{
    /// <summary>Qué ocurre cuando termina (o se omite) una cinemática.</summary>
    public enum CinematicEndBehaviour
    {
        /// <summary>Descarga la escena 3D y devuelve el control al juego 2D que la pidió (estado intacto).</summary>
        ReturnToGame = 0,

        /// <summary>Descarga la escena 3D y carga la escena indicada en 'Return Scene' (menú, créditos...).</summary>
        LoadScene = 1,
    }

    /// <summary>
    /// Configuración editable de UNA cinemática. Es el punto de sustitución de Nivel 3:
    /// para reemplazar una cinemática completa basta con apuntar 'Scene' (o 'Timeline Override')
    /// a la versión nueva; el identificador y la lógica del juego no cambian.
    /// </summary>
    [CreateAssetMenu(fileName = "CinematicDef_", menuName = "SOL-3/Cinemáticas/Definición de cinemática", order = 1)]
    public sealed class CinematicDefinition : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Identificador estable que usa la lógica del juego. No lo cambies.")]
        [CinematicId, SerializeField] string id = string.Empty;

        [Tooltip("Nombre descriptivo (solo informativo).")]
        [SerializeField] string displayName = string.Empty;

        [Tooltip("Notas para el equipo: cuándo se reproduce, estado, pendientes...")]
        [TextArea(2, 6), SerializeField] string notes = string.Empty;

        [Header("Contenido 3D")]
        [Tooltip("Escena 3D de la cinemática. Debe contener un CinematicSceneRoot y estar incluida en la build.")]
        [SerializeField] SceneReference scene = new SceneReference();

        [Tooltip("Opcional. Sustituye al Timeline del PlayableDirector de la escena sin abrirla. " +
                 "Sus pistas se enlazan por nombre (ver CinematicBindingKeys). " +
                 "Nota: este asset se carga junto con el catálogo.")]
        [SerializeField] TimelineAsset timelineOverride;

        [Tooltip("Duración prevista en segundos. Se usa para validar y como límite de seguridad si el Timeline no terminara.")]
        [Min(0f), SerializeField] float expectedDuration = 30f;

        [Header("Subtítulos y voces")]
        [Tooltip("Una hoja por idioma. Se elige la del idioma activo y, si no existe, la del idioma de respaldo.")]
        [SerializeField] List<SubtitleSheet> subtitleSheets = new List<SubtitleSheet>();

        [Header("Omisión")]
        [SerializeField] bool skippable = true;

        [Tooltip("Segundos tras el inicio a partir de los cuales se permite omitir (evita omisiones accidentales).")]
        [Min(0f), SerializeField] float skipAvailableAfter = 1f;

        [Header("Transiciones")]
        [Min(0f), SerializeField] float fadeInDuration = 0.75f;
        [Min(0f), SerializeField] float fadeOutDuration = 0.75f;
        [SerializeField] Color fadeColor = Color.black;

        [Header("Finalización")]
        [SerializeField] CinematicEndBehaviour endBehaviour = CinematicEndBehaviour.ReturnToGame;

        [Tooltip("Solo se usa si 'End Behaviour' es LoadScene.")]
        [SerializeField] SceneReference returnScene = new SceneReference();

        [Header("Repetición")]
        [Tooltip("Si está activo, la cinemática solo se reproduce una vez por partida guardada.")]
        [SerializeField] bool playOncePerSave = true;

        [Header("Recursos visuales (sustitución opcional, Nivel 1)")]
        [Tooltip("Sustituye el modelo visual de un actor de la escena (por su clave de enlace) sin modificar la escena. " +
                 "Nota: estos prefabs se cargan junto con el catálogo; para cambios permanentes edita el prefab del actor.")]
        [SerializeField] List<CastOverride> castOverrides = new List<CastOverride>();

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public string Notes => notes;
        public SceneReference Scene => scene;
        public TimelineAsset TimelineOverride => timelineOverride;
        public float ExpectedDuration => expectedDuration;
        public IReadOnlyList<SubtitleSheet> SubtitleSheets => subtitleSheets;
        public bool Skippable => skippable;
        public float SkipAvailableAfter => skipAvailableAfter;
        public float FadeInDuration => fadeInDuration;
        public float FadeOutDuration => fadeOutDuration;
        public Color FadeColor => fadeColor;
        public CinematicEndBehaviour EndBehaviour => endBehaviour;
        public SceneReference ReturnScene => returnScene;
        public bool PlayOncePerSave => playOncePerSave;
        public IReadOnlyList<CastOverride> CastOverrides => castOverrides;

        /// <summary>Devuelve la hoja de subtítulos del idioma pedido, la del idioma de respaldo o null.</summary>
        public SubtitleSheet GetSubtitles(string language, string fallbackLanguage)
        {
            SubtitleSheet fallback = null;
            for (int i = 0; i < subtitleSheets.Count; i++)
            {
                var sheet = subtitleSheets[i];
                if (sheet == null)
                    continue;
                if (string.Equals(sheet.Language, language, StringComparison.OrdinalIgnoreCase))
                    return sheet;
                if (fallback == null && string.Equals(sheet.Language, fallbackLanguage, StringComparison.OrdinalIgnoreCase))
                    fallback = sheet;
            }
            return fallback;
        }
    }

    /// <summary>Sustitución del modelo visual de un actor (clave de enlace → prefab).</summary>
    [Serializable]
    public sealed class CastOverride
    {
        [Tooltip("Clave del CinematicBindingTarget del actor, por ejemplo 'Astronaut' o 'AURA'.")]
        public string bindingKey = string.Empty;

        [Tooltip("Prefab del nuevo modelo visual. Se instancia dentro del 'Visual Root' del actor.")]
        public GameObject visualPrefab;
    }
}

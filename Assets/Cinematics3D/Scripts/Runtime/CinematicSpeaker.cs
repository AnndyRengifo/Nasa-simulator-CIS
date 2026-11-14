using UnityEngine;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Personaje que habla en los subtítulos (AURA, Control Tierra...).
    /// Separado de las líneas de diálogo para poder cambiar su nombre visible o su color
    /// en un solo sitio, sin tocar ninguna cinemática.
    /// </summary>
    [CreateAssetMenu(fileName = "SPK_", menuName = "SOL-3/Cinemáticas/Personaje que habla", order = 3)]
    public sealed class CinematicSpeaker : ScriptableObject
    {
        [Tooltip("Identificador interno estable, por ejemplo 'aura' o 'earth_control'.")]
        [SerializeField] string speakerId = string.Empty;

        [Tooltip("Nombre que se muestra en el subtítulo.")]
        [SerializeField] string displayName = string.Empty;

        [Tooltip("Color del nombre en el subtítulo.")]
        [SerializeField] Color nameColor = Color.white;

        public string SpeakerId => speakerId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Color NameColor => nameColor;
    }
}

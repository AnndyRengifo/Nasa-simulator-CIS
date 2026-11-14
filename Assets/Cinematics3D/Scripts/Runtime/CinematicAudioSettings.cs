using UnityEngine;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Volúmenes de las cinemáticas (0-1), guardados en PlayerPrefs. El menú de opciones del juego
    /// puede leerlos y cambiarlos. Si el catálogo tiene un AudioMixer, se aplican a sus parámetros
    /// expuestos; si no, el CinematicManager los aplica directamente a las fuentes de audio.
    /// </summary>
    public static class CinematicAudioSettings
    {
        const string Prefix = "SOL3.Cinematics.Volume.";

        public static float Master { get => Get("Master"); set => Set("Master", value); }
        public static float Music { get => Get("Music"); set => Set("Music", value); }
        public static float Voice { get => Get("Voice"); set => Set("Voice", value); }
        public static float Sfx { get => Get("SFX"); set => Set("SFX", value); }

        static float Get(string key) => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, 1f));

        static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }

        /// <summary>Convierte un volumen lineal (0-1) a decibelios para un AudioMixer.</summary>
        public static float ToDecibels(float linear) => linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Ambiente de un tramo de la cinemática (cielo, niebla y luz ambiente). Mientras el objeto está
    /// activo, su ambiente se aplica a la escena; al desactivarse se restaura el anterior.
    /// Se controla con pistas de activación del Timeline (por ejemplo "Env_Space" durante los
    /// planos en el espacio o "Env_Night" al caer la noche). Funciona también en la vista previa
    /// del Timeline en el Editor.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("SOL-3/Cinemáticas/Cinematic Environment Profile")]
    public sealed class CinematicEnvironmentProfile : MonoBehaviour
    {
        [SerializeField] Material skybox;
        [SerializeField] bool fog = true;
        [SerializeField] Color fogColor = new Color(0.74f, 0.5f, 0.34f);
        [Min(0f), SerializeField] float fogDensity = 0.006f;
        [SerializeField] Color ambientSky = new Color(0.62f, 0.47f, 0.39f);
        [SerializeField] Color ambientEquator = new Color(0.45f, 0.31f, 0.24f);
        [SerializeField] Color ambientGround = new Color(0.2f, 0.13f, 0.1f);

        struct Snapshot
        {
            public Material Skybox;
            public bool Fog;
            public Color FogColor;
            public float FogDensity;
            public FogMode FogMode;
            public AmbientMode AmbientMode;
            public Color Sky, Equator, Ground;
        }

        static readonly List<CinematicEnvironmentProfile> s_Active = new List<CinematicEnvironmentProfile>();
        static Snapshot s_Base;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Active.Clear();

        void OnEnable()
        {
            if (gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                return;
            if (s_Active.Count == 0)
                s_Base = Capture();
            s_Active.Remove(this);
            s_Active.Add(this);
            Apply();
        }

        void OnDisable()
        {
            s_Active.Remove(this);
            // Si la escena cinemática ya no es la activa (se está descargando), no se toca la iluminación
            // de la escena que ahora está activa (el juego 2D).
            if (gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene())
            {
                s_Active.Clear();
                return;
            }
            if (s_Active.Count > 0)
                s_Active[s_Active.Count - 1].Apply();
            else
                Restore(s_Base);
        }

        void Apply()
        {
            if (skybox != null)
                RenderSettings.skybox = skybox;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
        }

        static Snapshot Capture() => new Snapshot
        {
            Skybox = RenderSettings.skybox,
            Fog = RenderSettings.fog,
            FogColor = RenderSettings.fogColor,
            FogDensity = RenderSettings.fogDensity,
            FogMode = RenderSettings.fogMode,
            AmbientMode = RenderSettings.ambientMode,
            Sky = RenderSettings.ambientSkyColor,
            Equator = RenderSettings.ambientEquatorColor,
            Ground = RenderSettings.ambientGroundColor,
        };

        static void Restore(Snapshot snapshot)
        {
            RenderSettings.skybox = snapshot.Skybox;
            RenderSettings.fog = snapshot.Fog;
            RenderSettings.fogColor = snapshot.FogColor;
            RenderSettings.fogDensity = snapshot.FogDensity;
            RenderSettings.fogMode = snapshot.FogMode;
            RenderSettings.ambientMode = snapshot.AmbientMode;
            RenderSettings.ambientSkyColor = snapshot.Sky;
            RenderSettings.ambientEquatorColor = snapshot.Equator;
            RenderSettings.ambientGroundColor = snapshot.Ground;
        }
    }
}

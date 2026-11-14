using UnityEngine;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Ayuda para el equipo del juego 2D: permite pedir cinemáticas desde eventos del Inspector
    /// (botones, UnityEvents, triggers) sin escribir código. La lógica del juego sigue decidiendo
    /// CUÁNDO llamar a cada método (por ejemplo, PlayEndingSuccess solo tras comprobar la victoria).
    /// </summary>
    [AddComponentMenu("SOL-3/Cinemáticas/Cinematic Requester")]
    public sealed class CinematicRequester : MonoBehaviour
    {
        [CinematicId, SerializeField] string cinematicId = CinematicIds.Intro;
        [Tooltip("Pedir la cinemática automáticamente al iniciar la escena (p. ej. la intro de una partida nueva).")]
        [SerializeField] bool playOnStart;

        void Start()
        {
            if (playOnStart)
                Play();
        }

        public void Play() => Play(cinematicId);

        public void Play(string id)
        {
            var manager = CinematicManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning($"[Cinematics] No hay CinematicManager en la escena: no se puede reproducir '{id}'.", this);
                return;
            }
            manager.Play(id);
        }

        public void PlayIntro() => Play(CinematicIds.Intro);
        public void PlaySol1Night() => Play(CinematicIds.Sol1Night);
        public void PlaySolarAlert() => Play(CinematicIds.SolarAlert);
        public void PlaySol3Start() => Play(CinematicIds.Sol3Start);
        public void PlayEndingSuccess() => Play(CinematicIds.EndingSuccess);
        public void PlayEndingFailure() => Play(CinematicIds.EndingFailure);
        public void PlayEndingPartial() => Play(CinematicIds.EndingPartial);

        public void SkipCurrent() => CinematicManager.Instance?.Skip();
    }
}

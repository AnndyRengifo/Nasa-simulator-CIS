using System;
using UnityEngine;
using UnityEngine.Events;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Puente listo para usar entre el juego 2D y las cinemáticas, configurable sin código:
    /// • Eventos "On Starting" / "On Finished" para pausar y reanudar las mecánicas 2D
    ///   (por ejemplo, desactivar los controles o el temporizador del sol).
    /// • Registro de cinemáticas ya mostradas por partida (PlayerPrefs, por ranura).
    /// El juego puede sustituirlo por su propia implementación de <see cref="ICinematicGameBridge"/>
    /// (por ejemplo, guardando el registro en su sistema de partidas).
    /// </summary>
    [AddComponentMenu("SOL-3/Cinemáticas/Simple Cinematic Game Bridge")]
    public sealed class SimpleCinematicGameBridge : MonoBehaviour, ICinematicGameBridge
    {
        [Serializable] public sealed class StartingEvent : UnityEvent<string> { }
        [Serializable] public sealed class FinishedEvent : UnityEvent<string, CinematicEndReason> { }

        [Tooltip("Ranura de partida: separa el registro de cinemáticas vistas entre partidas.")]
        [SerializeField] string saveSlot = "default";

        [Tooltip("Se invoca antes de cargar la cinemática (pausa aquí las mecánicas 2D).")]
        [SerializeField] StartingEvent onStarting = new StartingEvent();

        [Tooltip("Se invoca cuando el control vuelve al juego (reanuda aquí las mecánicas 2D).")]
        [SerializeField] FinishedEvent onFinished = new FinishedEvent();

        public string SaveSlot { get => saveSlot; set => saveSlot = string.IsNullOrEmpty(value) ? "default" : value; }
        public StartingEvent OnStarting => onStarting;
        public FinishedEvent OnFinished => onFinished;

        string Key(string id) => $"SOL3.Cinematics.Shown.{saveSlot}.{id}";

        public void OnCinematicStarting(string cinematicId) => onStarting.Invoke(cinematicId);

        public void OnCinematicFinished(CinematicResult result) => onFinished.Invoke(result.CinematicId, result.Reason);

        public bool HasCinematicBeenShown(string cinematicId) => PlayerPrefs.GetInt(Key(cinematicId), 0) == 1;

        public void MarkCinematicShown(string cinematicId)
        {
            PlayerPrefs.SetInt(Key(cinematicId), 1);
            PlayerPrefs.Save();
        }

        /// <summary>Olvida las cinemáticas vistas de esta ranura (llamar al empezar una partida nueva).</summary>
        public void ResetShownCinematics()
        {
            foreach (var id in CinematicIds.All)
                PlayerPrefs.DeleteKey(Key(id));
            PlayerPrefs.Save();
        }
    }
}

using System;

namespace Sol3.Cinematics
{
    /// <summary>
    /// API pública que usará el juego 2D para pedir cinemáticas. La implementa CinematicManager
    /// (Fase E). Se define ya para que el juego 2D pueda programarse contra ella.
    ///
    /// Reglas del contrato:
    /// • La lógica del juego decide QUÉ cinemática corresponde; reproducirla nunca cambia el
    ///   resultado de la partida.
    /// • Solo hay una cinemática a la vez: una petición mientras otra se reproduce se rechaza.
    /// • Si Play devuelve true, onFinished se invoca exactamente una vez (completada, omitida,
    ///   detenida o fallida), así que el juego 2D nunca queda bloqueado.
    /// </summary>
    public interface ICinematicService
    {
        /// <summary>True mientras se carga, reproduce o descarga una cinemática.</summary>
        bool IsPlaying { get; }

        /// <summary>Identificador en curso, o cadena vacía.</summary>
        string CurrentCinematicId { get; }

        /// <summary>Se dispara cuando una cinemática empieza a mostrarse.</summary>
        event Action<string> CinematicStarted;

        /// <summary>Se dispara cuando el control ha vuelto al juego.</summary>
        event Action<CinematicResult> CinematicFinished;

        /// <summary>
        /// Solicita reproducir una cinemática por identificador.
        /// Devuelve false (sin invocar onFinished) si la petición se rechaza: identificador
        /// desconocido, otra cinemática en curso, o cinemática "una vez por partida" ya mostrada.
        /// </summary>
        bool Play(string cinematicId, Action<CinematicResult> onFinished = null);

        /// <summary>Omite la cinemática actual si su definición lo permite. Devuelve true si se omitió.</summary>
        bool Skip();

        /// <summary>Detiene la cinemática actual y devuelve el control al juego.</summary>
        void Stop();
    }
}

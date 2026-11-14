namespace Sol3.Cinematics
{
    /// <summary>Motivo por el que terminó una cinemática.</summary>
    public enum CinematicEndReason
    {
        /// <summary>Se reprodujo hasta el final.</summary>
        Completed = 0,

        /// <summary>El jugador la omitió.</summary>
        Skipped = 1,

        /// <summary>El juego la detuvo explícitamente.</summary>
        Stopped = 2,

        /// <summary>No se pudo reproducir (escena o Timeline ausente, error de carga...). La partida continúa igualmente.</summary>
        Failed = 3,
    }

    /// <summary>
    /// Resultado informativo de una reproducción. Es SOLO presentación: no contiene ni modifica
    /// el resultado de la partida. La victoria, la derrota o el final parcial los decide siempre
    /// la lógica del juego antes de pedir la cinemática correspondiente.
    /// </summary>
    public readonly struct CinematicResult
    {
        public readonly string CinematicId;
        public readonly CinematicEndReason Reason;
        public readonly string Message;

        public CinematicResult(string cinematicId, CinematicEndReason reason, string message = null)
        {
            CinematicId = cinematicId;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        /// <summary>True si el jugador llegó a ver la cinemática (completa u omitida).</summary>
        public bool WasShown => Reason == CinematicEndReason.Completed || Reason == CinematicEndReason.Skipped;

        public override string ToString() =>
            string.IsNullOrEmpty(Message) ? $"{CinematicId}: {Reason}" : $"{CinematicId}: {Reason} ({Message})";
    }
}

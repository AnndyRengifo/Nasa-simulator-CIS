namespace Sol3.Cinematics
{
    /// <summary>
    /// Contrato que IMPLEMENTA EL JUEGO 2D para integrarse con las cinemáticas.
    /// El sistema de cinemáticas no conoce recursos, objetivos ni progreso del jugador: solo avisa
    /// antes y después de cada secuencia para que el juego pause, guarde y restaure lo que necesite.
    /// </summary>
    public interface ICinematicGameBridge
    {
        /// <summary>
        /// Se llama ANTES de cargar la escena 3D. El juego debe pausar sus mecánicas 2D, guardar el
        /// estado necesario y desactivar su cámara y su AudioListener.
        /// </summary>
        void OnCinematicStarting(string cinematicId);

        /// <summary>
        /// Se llama DESPUÉS de descargar la escena 3D (también si falló o se omitió). El juego debe
        /// restaurar su estado, reactivar su cámara y reanudar las mecánicas.
        /// </summary>
        void OnCinematicFinished(CinematicResult result);

        /// <summary>Para las cinemáticas "una vez por partida": ¿ya se mostró en la partida actual?</summary>
        bool HasCinematicBeenShown(string cinematicId);

        /// <summary>Registra en la partida guardada que la cinemática ya se mostró.</summary>
        void MarkCinematicShown(string cinematicId);
    }
}

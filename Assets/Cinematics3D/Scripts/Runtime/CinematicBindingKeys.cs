namespace Sol3.Cinematics
{
    /// <summary>
    /// Claves de enlace estándar entre los Timelines y los objetos de la escena.
    ///
    /// CONVENCIÓN: el NOMBRE de una pista del Timeline es igual a la clave de un
    /// <see cref="CinematicBindingTarget"/> de la escena. Gracias a eso un Timeline nuevo
    /// (asignado desde el catálogo) se enlaza solo, sin abrir ni modificar la escena.
    ///
    /// Para las cámaras: cada clip de la pista Cinemachine ("Brain") debe llamarse igual que
    /// el GameObject de su CinemachineCamera (por ejemplo "CM_Intro_01_MarsOrbit").
    /// Para los clips de Control Track: el nombre del clip es el nombre del GameObject que controla.
    /// </summary>
    public static class CinematicBindingKeys
    {
        /// <summary>Pista Cinemachine → CinemachineBrain de la cámara de la cinemática.</summary>
        public const string Brain = "Brain";

        /// <summary>Pista de audio de música → AudioSource "Music".</summary>
        public const string Music = "Music";

        /// <summary>Pista de audio ambiente → AudioSource "Ambience".</summary>
        public const string Ambience = "Ambience";

        /// <summary>Pista de efectos de sonido → AudioSource "SFX".</summary>
        public const string Sfx = "SFX";

        /// <summary>Pista de animación del astronauta.</summary>
        public const string Astronaut = "Astronaut";

        /// <summary>Pista de animación de AURA (holograma).</summary>
        public const string Aura = "AURA";

        /// <summary>Pista de las pantallas / comunicación de Control Tierra.</summary>
        public const string EarthControl = "EarthControl";
    }
}

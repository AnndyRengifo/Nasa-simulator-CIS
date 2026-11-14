using System;
using System.Collections.Generic;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Identificadores ESTABLES de las cinemáticas de SOL-3: Base Aurora.
    /// La lógica del juego 2D, el catálogo central y las partidas guardadas dependen de estos valores:
    /// no los cambies nunca. Para sustituir una cinemática se cambia su definición en el catálogo,
    /// no su identificador.
    /// </summary>
    public static class CinematicIds
    {
        /// <summary>Presentación de Marte, Base Aurora, el astronauta y AURA. Al iniciar una partida nueva.</summary>
        public const string Intro = "intro";

        /// <summary>Llegada de la primera noche marciana. Al comenzar la primera noche.</summary>
        public const string Sol1Night = "sol1_night";

        /// <summary>Alerta por actividad solar. Cuando la lógica del juego activa el evento solar.</summary>
        public const string SolarAlert = "solar_alert";

        /// <summary>Inicio del tercer sol. Al comenzar el tercer sol.</summary>
        public const string Sol3Start = "sol3_start";

        /// <summary>Final exitoso. Solo cuando la lógica del juego determina la victoria.</summary>
        public const string EndingSuccess = "ending_success";

        /// <summary>Final de fracaso. Solo cuando la lógica del juego determina la derrota.</summary>
        public const string EndingFailure = "ending_failure";

        /// <summary>Final parcial. Solo cuando la lógica del juego determina el resultado parcial.</summary>
        public const string EndingPartial = "ending_partial";

        static readonly string[] s_All =
        {
            Intro, Sol1Night, SolarAlert, Sol3Start, EndingSuccess, EndingFailure, EndingPartial
        };

        /// <summary>Los siete identificadores, en orden narrativo.</summary>
        public static IReadOnlyList<string> All => s_All;

        /// <summary>True si el identificador es uno de los siete oficiales.</summary>
        public static bool IsKnown(string id) => !string.IsNullOrEmpty(id) && Array.IndexOf(s_All, id) >= 0;

        /// <summary>True para las tres cinemáticas de final.</summary>
        public static bool IsEnding(string id) => id == EndingSuccess || id == EndingFailure || id == EndingPartial;
    }
}

using System;

namespace Sol3.Game.Sim
{
    /// <summary>
    /// Reglas de la simulación (archivo Resources/Data/sim_config.json).
    /// SIMULACIÓN: todos estos valores son simplificados para que el juego sea jugable y justo.
    /// No son datos oficiales de NASA ni de ninguna agencia.
    /// </summary>
    [Serializable]
    public sealed class SimConfig
    {
        public string note = "SIMULACIÓN: valores simplificados creados para la jugabilidad. No son datos oficiales.";
        public float solSeconds = 210f;
        public float dayFraction = 0.58f;
        public int sols = 3;
        public StartValues start = new StartValues();
        public Rates rates = new Rates();
        public Goals goals = new Goals();
        public EventPacing events = new EventPacing();
    }

    [Serializable]
    public sealed class StartValues
    {
        public float energy = 78f;
        public float oxygen = 85f;
        public float water = 80f;
        public float food = 72f;
        public float health = 100f;
        public float radiation = 4f;
        public int spareParts = 4;
    }

    [Serializable]
    public sealed class Rates
    {
        // Energía (% de batería por segundo de juego)
        public float solarMax = 2.3f;
        public float baseLoad = 0.2f;
        public float lifeSupportLoad = 0.3f;
        public float recyclerLoad = 0.16f;
        public float greenhouseLoad = 0.2f;
        public float labLoad = 0.55f;
        public float commsLoad = 0.25f;
        public float shelterLoad = 0.12f;

        // Soporte vital
        public float oxygenProduction = 0.62f;
        public float oxygenUse = 0.34f;
        public float waterRecycle = 0.27f;
        public float waterUse = 0.15f;
        public float greenhouseWater = 0.09f;
        public float foodUse = 0.105f;

        // Cultivos
        public float cropSeconds = 65f;
        public float cropYield = 18f;
        public float plantWaterCost = 4f;
        public float witherSeconds = 25f;

        // Tripulación
        public float healthLoss = 0.75f;
        public float healthRegen = 0.12f;
        public float radiationStorm = 1.4f;
        public float radiationSheltered = 0.1f;
        public float radiationDecay = 0.04f;
        public float radiationHarm = 55f;

        // Desgaste de equipos (integridad por segundo de uso)
        public float wear = 0.0016f;
        public float overloadWear = 0.006f;

        // Investigación y comunicaciones
        public float experimentSeconds = 26f;
        public float transmissionSeconds = 38f;
        public float failedRepairEnergyCost = 3f;
    }

    [Serializable]
    public sealed class Goals
    {
        public int research = 3;
    }

    [Serializable]
    public sealed class EventPacing
    {
        public float checkInterval = 4f;
        public float minGapSeconds = 32f;
        public int[] maxEventsPerSol = { 2, 4, 5 };
        public float tutorialFaultAt = 35f;
        public float stormWarningSeconds = 22f;
        public float stormDurationSeconds = 45f;
    }
}

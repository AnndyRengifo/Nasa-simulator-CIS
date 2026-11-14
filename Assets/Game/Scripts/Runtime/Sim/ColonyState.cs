using System;
using System.Collections.Generic;

namespace Sol3.Game.Sim
{
    public enum SystemId
    {
        Solar,
        Batteries,
        LifeSupport,
        Recycler,
        Greenhouse,
        Lab,
        Comms,
        Shelter,
    }

    public enum DayPhase
    {
        Day,
        Night,
    }

    public enum ReportStatus
    {
        NotSent,
        Transmitting,
        Delivered,
    }

    public enum MissionOutcome
    {
        InProgress,
        Success,
        Partial,
        Failure,
    }

    public sealed class SystemState
    {
        public SystemId Id;
        public bool Powered = true;
        public bool Broken;
        public float Integrity = 1f;
    }

    public sealed class Crop
    {
        public bool Planted;
        public float Progress;
        public float DryTime;
        public bool Ready => Planted && Progress >= 1f;
    }

    /// <summary>Estado completo de la colonia. Sin dependencias de Unity: se puede probar y reiniciar.</summary>
    public sealed class ColonyState
    {
        public float Energy, Oxygen, Water, Food, Health, Radiation;
        public int SpareParts;

        public float Time;
        public int Sol = 1;
        public DayPhase Phase = DayPhase.Day;
        public float Daylight = 1f;
        public bool Blackout;

        public readonly Dictionary<SystemId, SystemState> Systems = new Dictionary<SystemId, SystemState>();
        public readonly Crop[] Crops = { new Crop(), new Crop(), new Crop() };

        public bool ShelterActive;
        public bool StormActive;

        public int Research;
        public int ExperimentIndex = -1;
        public float ExperimentProgress;
        public bool ExperimentReady => ExperimentIndex >= 0 && ExperimentProgress >= 1f;
        public readonly List<int> CompletedExperiments = new List<int>();

        public ReportStatus Report = ReportStatus.NotSent;
        public float TransmissionRemaining;
        public bool AntennaAligned;

        public MissionOutcome Outcome = MissionOutcome.InProgress;
        public string OutcomeReason = string.Empty;

        // Estadísticas para el informe final.
        public float MinEnergy = 100f, MinOxygen = 100f, MinWater = 100f, MinFood = 100f, MinHealth = 100f, MaxRadiation;
        public float EnergyUsedToday;
        public int Repairs, FailedRepairs, Blackouts, Harvests, EventsHandled, StormsSheltered, StormsUnsheltered;
        public readonly List<string> Decisions = new List<string>();
        public bool VisitedControl;
        public readonly float[] EnergyAtNight = { -1f, -1f, -1f, -1f };

        public ColonyState()
        {
            foreach (SystemId id in Enum.GetValues(typeof(SystemId)))
                Systems[id] = new SystemState { Id = id, Powered = id != SystemId.Shelter };
        }

        public SystemState this[SystemId id] => Systems[id];

        /// <summary>Un sistema funciona si tiene energía asignada, no está averiado y no hay apagón.</summary>
        public bool Works(SystemId id) => Systems[id].Powered && !Systems[id].Broken && !Blackout;

        /// <summary>Valor numérico por nombre (lo usan las condiciones del motor de eventos).</summary>
        public float Get(string stat)
        {
            switch (stat)
            {
                case "energy": return Energy;
                case "oxygen": return Oxygen;
                case "water": return Water;
                case "food": return Food;
                case "health": return Health;
                case "radiation": return Radiation;
                case "sol": return Sol;
                case "night": return Phase == DayPhase.Night ? 1f : 0f;
                case "research": return Research;
                case "parts": return SpareParts;
                case "energyUsedToday": return EnergyUsedToday;
                case "shelter": return ShelterActive ? 1f : 0f;
                case "experimentRunning": return ExperimentIndex >= 0 && !ExperimentReady ? 1f : 0f;
            }

            if (stat.StartsWith("integrity_", StringComparison.Ordinal) &&
                Enum.TryParse(stat.Substring(10), true, out SystemId id))
                return Systems[id].Integrity * 100f;
            if (stat.StartsWith("broken_", StringComparison.Ordinal) &&
                Enum.TryParse(stat.Substring(7), true, out SystemId brokenId))
                return Systems[brokenId].Broken ? 1f : 0f;
            return 0f;
        }

        public void Add(string stat, float delta)
        {
            switch (stat)
            {
                case "energy": Energy = Clamp(Energy + delta); break;
                case "oxygen": Oxygen = Clamp(Oxygen + delta); break;
                case "water": Water = Clamp(Water + delta); break;
                case "food": Food = Clamp(Food + delta); break;
                case "health": Health = Clamp(Health + delta); break;
                case "radiation": Radiation = Clamp(Radiation + delta); break;
                case "parts": SpareParts = Math.Max(0, SpareParts + (int)Math.Round(delta)); break;
            }
        }

        public static float Clamp(float value) => value < 0f ? 0f : value > 100f ? 100f : value;
    }
}

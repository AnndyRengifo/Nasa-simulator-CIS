using System;
using System.Collections.Generic;

namespace Sol3.Game.Sim
{
    public enum SimMessageKind
    {
        Info,
        SolStarted,
        NightStarted,
        DayStarted,
        Blackout,
        PowerRestored,
        EventWarning,
        EventStarted,
        EventResolved,
        DecisionRequired,
        StormStarted,
        StormEnded,
        CropReady,
        CropWithered,
        ExperimentReady,
        ReportDelivered,
        ReportLost,
        LowResource,
        MissionEnded,
    }

    public readonly struct SimMessage
    {
        public readonly SimMessageKind Kind;
        public readonly string Text;
        public readonly string Id;

        public SimMessage(SimMessageKind kind, string text, string id = null)
        {
            Kind = kind;
            Text = text;
            Id = id ?? string.Empty;
        }
    }

    /// <summary>
    /// Motor de simulación de la colonia (independiente de Unity). Avanza el tiempo, produce y consume
    /// recursos, desgasta los equipos, lanza eventos según el estado y decide el resultado de la misión.
    /// La capa gráfica solo llama a Tick() y a las acciones públicas, y lee los mensajes.
    /// </summary>
    public sealed class ColonySimulation
    {
        public readonly SimConfig Config;
        public readonly ColonyState State = new ColonyState();
        public readonly EventEngine Events;
        public readonly List<SimMessage> Messages = new List<SimMessage>();

        readonly Random m_Random;
        readonly HashSet<string> m_LowWarned = new HashSet<string>();

        public ColonySimulation(SimConfig config, EventCatalog events, int seed)
        {
            Config = config ?? new SimConfig();
            m_Random = new Random(seed);
            var start = Config.start;
            State.Energy = start.energy;
            State.Oxygen = start.oxygen;
            State.Water = start.water;
            State.Food = start.food;
            State.Health = start.health;
            State.Radiation = start.radiation;
            State.SpareParts = start.spareParts;
            Events = new EventEngine(this, events ?? new EventCatalog(), m_Random);
            UpdateClock();
        }

        public float MissionSeconds => Config.solSeconds * Config.sols;
        public float SolProgress => (State.Time % Config.solSeconds) / Config.solSeconds;
        public bool Ended => State.Outcome != MissionOutcome.InProgress;

        public void Post(SimMessageKind kind, string text, string id = null) => Messages.Add(new SimMessage(kind, text, id));

        // ================================================================== Tiempo

        public void Tick(float dt)
        {
            if (Ended || dt <= 0f)
                return;
            dt = Math.Min(dt, 0.25f);
            var s = State;
            var r = Config.rates;

            int previousSol = s.Sol;
            var previousPhase = s.Phase;
            s.Time += dt;
            UpdateClock();
            if (s.Sol != previousSol)
            {
                s.EnergyUsedToday = 0f;
                Post(SimMessageKind.SolStarted, $"Comienza el Sol {s.Sol}.");
            }
            else if (s.Phase != previousPhase)
            {
                if (s.Phase == DayPhase.Night && s.Sol < s.EnergyAtNight.Length)
                    s.EnergyAtNight[s.Sol] = s.Energy;
                Post(s.Phase == DayPhase.Night ? SimMessageKind.NightStarted : SimMessageKind.DayStarted,
                    s.Phase == DayPhase.Night ? "Cae la noche: los paneles solares dejan de producir." : "Amanece: los paneles solares vuelven a producir.");
            }

            // Energía
            float production = Production();
            float load = Load();
            s.EnergyUsedToday += load * dt;
            s.Energy = ColonyState.Clamp(s.Energy + (production - load) * dt);
            if (s.Energy <= 0f && !s.Blackout)
            {
                s.Blackout = true;
                s.Blackouts++;
                Post(SimMessageKind.Blackout, "¡Apagón! Las baterías están vacías y los sistemas se detienen.");
            }
            else if (s.Blackout && s.Energy > 4f)
            {
                s.Blackout = false;
                Post(SimMessageKind.PowerRestored, "Energía restablecida.");
            }

            // Soporte vital
            s.Oxygen = ColonyState.Clamp(s.Oxygen + ((s.Works(SystemId.LifeSupport) ? r.oxygenProduction * (0.5f + 0.5f * s[SystemId.LifeSupport].Integrity) : 0f) - r.oxygenUse) * dt);
            bool growing = false;
            foreach (var crop in s.Crops)
                growing |= crop.Planted && !crop.Ready;
            float waterIn = s.Works(SystemId.Recycler) ? r.waterRecycle * (0.5f + 0.5f * s[SystemId.Recycler].Integrity) : 0f;
            float waterOut = r.waterUse + (growing && s.Works(SystemId.Greenhouse) ? r.greenhouseWater : 0f);
            s.Water = ColonyState.Clamp(s.Water + (waterIn - waterOut) * dt);
            s.Food = ColonyState.Clamp(s.Food - r.foodUse * dt);

            TickCrops(dt);

            // Radiación
            if (s.StormActive)
                s.Radiation = ColonyState.Clamp(s.Radiation + (s.ShelterActive ? r.radiationSheltered : r.radiationStorm) * dt);
            else
                s.Radiation = ColonyState.Clamp(s.Radiation - r.radiationDecay * dt);

            // Salud
            int problems = 0;
            if (s.Oxygen < 18f) problems += s.Oxygen <= 0f ? 3 : 1;
            if (s.Water < 10f) problems++;
            if (s.Food < 8f) problems++;
            if (s.Radiation > r.radiationHarm) problems++;
            if (problems > 0)
                s.Health = ColonyState.Clamp(s.Health - r.healthLoss * problems * dt);
            else if (s.Oxygen > 35f && s.Water > 25f && s.Food > 20f)
                s.Health = ColonyState.Clamp(s.Health + r.healthRegen * dt);

            TickWear(dt);
            TickResearch(dt);
            TickReport(dt);
            WarnLowResources();
            TrackStats();

            Events.Tick(dt);

            if (s.Health <= 0f)
                End(MissionOutcome.Failure, "La salud de la tripulación llegó a un estado crítico y la misión tuvo que interrumpirse.");
            else if (s.Time >= MissionSeconds)
                EvaluateEnd();
        }

        /// <summary>Producción solar actual (unidades de energía por segundo de juego).</summary>
        public float Production()
        {
            var s = State;
            return s[SystemId.Solar].Broken ? 0f : Config.rates.solarMax * s.Daylight * (0.4f + 0.6f * s[SystemId.Solar].Integrity);
        }

        /// <summary>Consumo actual de todos los sistemas encendidos.</summary>
        public float Load()
        {
            var s = State;
            var r = Config.rates;
            float load = r.baseLoad;
            if (s[SystemId.LifeSupport].Powered && !s[SystemId.LifeSupport].Broken) load += r.lifeSupportLoad;
            if (s[SystemId.Recycler].Powered && !s[SystemId.Recycler].Broken) load += r.recyclerLoad;
            if (s[SystemId.Greenhouse].Powered && !s[SystemId.Greenhouse].Broken) load += r.greenhouseLoad;
            if (s[SystemId.Lab].Powered && s.ExperimentIndex >= 0 && !s.ExperimentReady && !s.ShelterActive) load += r.labLoad;
            if (s.Report == ReportStatus.Transmitting) load += r.commsLoad;
            if (s.ShelterActive) load += r.shelterLoad;
            if (s[SystemId.Batteries].Broken) load *= 1.35f; // baterías averiadas: pérdidas
            return load;
        }

        void UpdateClock()
        {
            var s = State;
            float solTime = s.Time % Config.solSeconds;
            s.Sol = Math.Min(Config.sols, (int)(s.Time / Config.solSeconds) + 1);
            float dayLength = Config.solSeconds * Config.dayFraction;
            s.Phase = solTime < dayLength ? DayPhase.Day : DayPhase.Night;
            if (s.Phase == DayPhase.Day)
            {
                float t = solTime / dayLength;
                s.Daylight = (float)Math.Sin(Math.PI * Math.Min(1.0, Math.Max(0.0, 0.08 + t * 0.84)));
            }
            else
            {
                s.Daylight = 0f;
            }
        }

        /// <summary>Hora marciana simulada (00:00-24:39) para mostrar en pantalla.</summary>
        public string ClockText()
        {
            float solTime = State.Time % Config.solSeconds;
            // El día jugable va de 06:00 a 19:00; la noche de 19:00 a 06:00.
            float dayLength = Config.solSeconds * Config.dayFraction;
            double hours = solTime < dayLength
                ? 6.0 + 13.0 * solTime / dayLength
                : 19.0 + 11.0 * (solTime - dayLength) / (Config.solSeconds - dayLength);
            hours %= 24.0;
            int h = (int)hours;
            int m = (int)((hours - h) * 60.0);
            return $"{h:00}:{m:00}";
        }

        void TickCrops(float dt)
        {
            var s = State;
            var r = Config.rates;
            bool lights = s.Works(SystemId.Greenhouse);
            for (int i = 0; i < s.Crops.Length; i++)
            {
                var crop = s.Crops[i];
                if (!crop.Planted || crop.Ready)
                    continue;
                if (lights && s.Water > 0f && !s.ShelterActive)
                {
                    crop.Progress = Math.Min(1f, crop.Progress + dt / r.cropSeconds);
                    crop.DryTime = 0f;
                    if (crop.Ready)
                        Post(SimMessageKind.CropReady, $"Cultivo {i + 1} listo para cosechar.");
                }
                else if (s.Water <= 0f || !lights)
                {
                    crop.DryTime += dt;
                    if (crop.DryTime > r.witherSeconds)
                    {
                        crop.Planted = false;
                        crop.Progress = 0f;
                        crop.DryTime = 0f;
                        Post(SimMessageKind.CropWithered, $"El cultivo {i + 1} se marchitó por falta de agua o de luz.");
                    }
                }
            }
        }

        void TickWear(float dt)
        {
            var s = State;
            var r = Config.rates;
            float wear = r.wear + (s.Energy < 15f ? r.overloadWear : 0f);
            foreach (var system in s.Systems.Values)
            {
                if (!system.Broken && system.Powered && system.Id != SystemId.Shelter)
                    system.Integrity = Math.Max(0.15f, system.Integrity - wear * dt);
            }
        }

        void TickResearch(float dt)
        {
            var s = State;
            if (s.ExperimentIndex < 0 || s.ExperimentReady || s.ShelterActive || !s.Works(SystemId.Lab))
                return;
            s.ExperimentProgress = Math.Min(1f, s.ExperimentProgress + dt / Config.rates.experimentSeconds);
            if (s.ExperimentReady)
                Post(SimMessageKind.ExperimentReady, "Experimento terminado: registra los resultados en el laboratorio.");
        }

        void TickReport(float dt)
        {
            var s = State;
            if (s.Report != ReportStatus.Transmitting)
                return;
            if (!s.Works(SystemId.Comms))
            {
                s.Report = ReportStatus.NotSent;
                s.AntennaAligned = false;
                Post(SimMessageKind.ReportLost, "Se perdió la transmisión: el sistema de comunicaciones dejó de funcionar. Hay que repararlo y reenviar.");
                return;
            }
            s.TransmissionRemaining -= dt;
            if (s.TransmissionRemaining <= 0f)
            {
                s.Report = ReportStatus.Delivered;
                s.SpareParts += 2;
                Post(SimMessageKind.ReportDelivered, "Control Tierra confirma la recepción del informe científico y envía instrucciones con 2 repuestos.");
            }
        }

        void WarnLowResources()
        {
            var s = State;
            Warn("energy", s.Energy < 20f, "Batería por debajo del 20 %: reduce el consumo.");
            Warn("oxygen", s.Oxygen < 25f, "Oxígeno bajo: revisa el soporte vital.");
            Warn("water", s.Water < 20f, "Agua baja: revisa el reciclador.");
            Warn("food", s.Food < 18f, "Comida baja: cosecha en el invernadero.");
            Warn("radiation", s.Radiation > Config.rates.radiationHarm * 0.8f, "Radiación alta: protege a la tripulación en el refugio.");
        }

        void Warn(string key, bool condition, string text)
        {
            if (condition && m_LowWarned.Add(key))
                Post(SimMessageKind.LowResource, text, key);
            else if (!condition)
                m_LowWarned.Remove(key);
        }

        void TrackStats()
        {
            var s = State;
            s.MinEnergy = Math.Min(s.MinEnergy, s.Energy);
            s.MinOxygen = Math.Min(s.MinOxygen, s.Oxygen);
            s.MinWater = Math.Min(s.MinWater, s.Water);
            s.MinFood = Math.Min(s.MinFood, s.Food);
            s.MinHealth = Math.Min(s.MinHealth, s.Health);
            s.MaxRadiation = Math.Max(s.MaxRadiation, s.Radiation);
        }

        void EvaluateEnd()
        {
            var s = State;
            bool research = s.Research >= Config.goals.research;
            bool report = s.Report == ReportStatus.Delivered;
            if (research && report)
                End(MissionOutcome.Success, "La tripulación sobrevivió tres soles, completó la investigación y la Tierra recibió el informe científico.");
            else
                End(MissionOutcome.Partial, research
                    ? "La tripulación está a salvo y la investigación se completó, pero el informe no llegó a la Tierra."
                    : $"La tripulación está a salvo, pero solo se completaron {s.Research} de {Config.goals.research} investigaciones" +
                      (report ? "." : " y el informe no se envió."));
        }

        void End(MissionOutcome outcome, string reason)
        {
            if (Ended)
                return;
            State.Outcome = outcome;
            State.OutcomeReason = reason;
            Post(SimMessageKind.MissionEnded, reason);
        }

        // ================================================================== Acciones del jugador

        public void SetPower(SystemId id, bool on)
        {
            if (id == SystemId.Solar || id == SystemId.Batteries)
                return;
            State[id].Powered = on;
            State.Decisions.Add($"Sol {State.Sol}: {(on ? "encendió" : "apagó")} {SystemNames.Get(id)}.");
        }

        public bool CanRepair(SystemId id) => State[id].Broken || State[id].Integrity < 0.6f;

        /// <summary>Reparación completada en su minijuego. Consume un repuesto si hay.</summary>
        public void CompleteRepair(SystemId id)
        {
            var system = State[id];
            bool wasBroken = system.Broken;
            system.Broken = false;
            system.Integrity = State.SpareParts > 0 ? 1f : 0.75f;
            if (State.SpareParts > 0)
                State.SpareParts--;
            State.Repairs++;
            if (wasBroken)
                Events.NotifyRepaired(id);
            Post(SimMessageKind.Info, $"{SystemNames.Get(id)} reparado.");
        }

        public void FailRepair(SystemId id)
        {
            State.FailedRepairs++;
            State.Energy = ColonyState.Clamp(State.Energy - Config.rates.failedRepairEnergyCost);
            State[id].Integrity = Math.Max(0.15f, State[id].Integrity - 0.05f);
        }

        public bool Plant(int index)
        {
            var crop = State.Crops[index];
            if (crop.Planted || State.Water < Config.rates.plantWaterCost)
                return false;
            State.Water -= Config.rates.plantWaterCost;
            crop.Planted = true;
            crop.Progress = 0f;
            return true;
        }

        public bool Harvest(int index)
        {
            var crop = State.Crops[index];
            if (!crop.Ready)
                return false;
            crop.Planted = false;
            crop.Progress = 0f;
            State.Food = ColonyState.Clamp(State.Food + Config.rates.cropYield);
            State.Harvests++;
            return true;
        }

        public bool StartExperiment(int index)
        {
            if (State.ExperimentIndex >= 0 || State.CompletedExperiments.Contains(index))
                return false;
            State.ExperimentIndex = index;
            State.ExperimentProgress = 0f;
            return true;
        }

        public bool CollectExperiment()
        {
            if (!State.ExperimentReady)
                return false;
            State.CompletedExperiments.Add(State.ExperimentIndex);
            State.Research++;
            State.ExperimentIndex = -1;
            State.ExperimentProgress = 0f;
            if (State.Research == 2)
                State.SpareParts++;
            return true;
        }

        public void SetShelter(bool active)
        {
            if (State.ShelterActive == active)
                return;
            State.ShelterActive = active;
            State.Decisions.Add($"Sol {State.Sol}: {(active ? "activó" : "desactivó")} el protocolo de refugio.");
        }

        public bool CanSendReport => State.Research >= Config.goals.research && State.Report == ReportStatus.NotSent && State.Works(SystemId.Comms);

        public bool SendReport()
        {
            if (!CanSendReport || !State.AntennaAligned)
                return false;
            State.Report = ReportStatus.Transmitting;
            State.TransmissionRemaining = Config.rates.transmissionSeconds;
            State.Decisions.Add($"Sol {State.Sol}: envió el informe científico a la Tierra.");
            return true;
        }
    }

    public static class SystemNames
    {
        public static string Get(SystemId id)
        {
            switch (id)
            {
                case SystemId.Solar: return "los paneles solares";
                case SystemId.Batteries: return "las baterías";
                case SystemId.LifeSupport: return "el generador de oxígeno";
                case SystemId.Recycler: return "el reciclador de agua";
                case SystemId.Greenhouse: return "la iluminación del invernadero";
                case SystemId.Lab: return "el laboratorio";
                case SystemId.Comms: return "las comunicaciones";
                default: return "el refugio";
            }
        }

        public static string Title(SystemId id)
        {
            switch (id)
            {
                case SystemId.Solar: return "Paneles solares";
                case SystemId.Batteries: return "Baterías";
                case SystemId.LifeSupport: return "Oxígeno";
                case SystemId.Recycler: return "Reciclaje de agua";
                case SystemId.Greenhouse: return "Luces invernadero";
                case SystemId.Lab: return "Laboratorio";
                case SystemId.Comms: return "Comunicaciones";
                default: return "Refugio";
            }
        }
    }
}

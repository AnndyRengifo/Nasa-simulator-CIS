using System;
using System.Collections.Generic;

namespace Sol3.Game.Sim
{
    [Serializable]
    public sealed class EventCatalog
    {
        public EventDef[] events = Array.Empty<EventDef>();
    }

    [Serializable]
    public sealed class EventCondition
    {
        public string stat;
        public string op = "<";
        public float value;
        public float weight;

        public bool Test(ColonyState state)
        {
            float v = state.Get(stat);
            switch (op)
            {
                case "<": return v < value;
                case "<=": return v <= value;
                case ">": return v > value;
                case ">=": return v >= value;
                case "==": return Math.Abs(v - value) < 0.001f;
                default: return false;
            }
        }
    }

    [Serializable]
    public sealed class StatEffect
    {
        public string stat;
        public float delta;
    }

    [Serializable]
    public sealed class EventOption
    {
        public string id;
        public string label;
        public string explanation;
        public StatEffect[] effects = Array.Empty<StatEffect>();
        public string powerOff = string.Empty;
        public bool shelter;
    }

    /// <summary>
    /// Definición de una emergencia (Resources/Data/events.json): identificador, descripción, condiciones
    /// de activación, severidad, opciones con costes y consecuencias, y explicación científica.
    /// </summary>
    [Serializable]
    public sealed class EventDef
    {
        public string id;
        public string kind = "breakdown"; // breakdown | decision | storm
        public string system = string.Empty;
        public string title;
        public string description;
        public string warning;
        public float warningSeconds;
        public int severity = 1;
        public int minSol = 1;
        public int maxSol = 3;
        public string phase = "any";
        public float cooldown = 90f;
        public bool once;
        public float baseWeight = 1f;
        public int forceSol;
        public float forceAt = -1f;
        public EventCondition[] conditions = Array.Empty<EventCondition>();
        public EventCondition[] boosters = Array.Empty<EventCondition>();
        public EventOption[] options = Array.Empty<EventOption>();
        public string science;

        public SystemId TargetSystem => Enum.TryParse(system, true, out SystemId id) ? id : SystemId.Batteries;
    }

    public enum EventStage
    {
        Warning,
        Active,
        Resolved,
    }

    public sealed class ActiveEvent
    {
        public EventDef Def;
        public EventStage Stage;
        public float Timer;
        public int Sol;
    }

    /// <summary>
    /// Motor de eventos independiente de los gráficos. La probabilidad de cada emergencia depende del
    /// estado actual (efecto dominó): equipos desgastados, baterías bajas o mucho consumo aumentan el riesgo.
    /// Siempre hay aviso o tiempo de reacción antes de las consecuencias graves.
    /// </summary>
    public sealed class EventEngine
    {
        readonly ColonySimulation m_Sim;
        readonly EventCatalog m_Catalog;
        readonly Random m_Random;
        readonly Dictionary<string, float> m_LastFired = new Dictionary<string, float>();
        readonly HashSet<string> m_Fired = new HashSet<string>();
        readonly int[] m_PerSol = new int[8];
        float m_CheckTimer;
        float m_LastEventTime = -999f;

        public readonly List<ActiveEvent> Active = new List<ActiveEvent>();
        public ActiveEvent PendingDecision { get; private set; }
        public ActiveEvent CurrentStorm { get; private set; }

        public EventEngine(ColonySimulation sim, EventCatalog catalog, Random random)
        {
            m_Sim = sim;
            m_Catalog = catalog;
            m_Random = random;
        }

        public IEnumerable<EventDef> Definitions => m_Catalog.events;

        public void Tick(float dt)
        {
            var state = m_Sim.State;
            for (int i = Active.Count - 1; i >= 0; i--)
                Advance(Active[i], dt);
            Active.RemoveAll(e => e.Stage == EventStage.Resolved);

            float solTime = state.Time % m_Sim.Config.solSeconds;
            foreach (var def in m_Catalog.events)
            {
                if (def.forceSol == state.Sol && def.forceAt >= 0f && solTime >= def.forceAt && !m_Fired.Contains(def.id + "@" + state.Sol))
                {
                    m_Fired.Add(def.id + "@" + state.Sol);
                    Fire(def);
                }
            }

            m_CheckTimer += dt;
            if (m_CheckTimer < m_Sim.Config.events.checkInterval)
                return;
            m_CheckTimer = 0f;

            var pacing = m_Sim.Config.events;
            int solIndex = Math.Max(0, Math.Min(pacing.maxEventsPerSol.Length - 1, state.Sol - 1));
            if (state.Time - m_LastEventTime < pacing.minGapSeconds || m_PerSol[state.Sol] >= pacing.maxEventsPerSol[solIndex] || PendingDecision != null)
                return;

            float total = 0f;
            var candidates = new List<(EventDef def, float weight)>();
            foreach (var def in m_Catalog.events)
            {
                float weight = Weight(def, state);
                if (weight <= 0f)
                    continue;
                candidates.Add((def, weight));
                total += weight;
            }
            if (candidates.Count == 0 || m_Random.NextDouble() > Math.Min(0.55, total * 0.02))
                return;

            double pick = m_Random.NextDouble() * total;
            foreach (var candidate in candidates)
            {
                pick -= candidate.weight;
                if (pick <= 0)
                {
                    Fire(candidate.def);
                    return;
                }
            }
        }

        float Weight(EventDef def, ColonyState state)
        {
            if (def.forceAt >= 0f && def.forceSol > 0)
                return 0f; // los eventos programados no se repiten al azar
            if (state.Sol < def.minSol || state.Sol > def.maxSol)
                return 0f;
            if (def.phase == "day" && state.Phase != DayPhase.Day || def.phase == "night" && state.Phase != DayPhase.Night)
                return 0f;
            if (def.once && m_Fired.Contains(def.id))
                return 0f;
            if (m_LastFired.TryGetValue(def.id, out float last) && state.Time - last < def.cooldown)
                return 0f;
            if (IsRunning(def.id))
                return 0f;
            foreach (var condition in def.conditions)
            {
                if (!condition.Test(state))
                    return 0f;
            }

            float weight = def.baseWeight;
            foreach (var booster in def.boosters)
            {
                if (booster.Test(state))
                    weight += booster.weight;
            }
            if (def.kind == "breakdown")
            {
                var system = state[def.TargetSystem];
                if (system.Broken)
                    return 0f;
                weight += (1f - system.Integrity) * 8f; // efecto dominó: el desgaste aumenta el riesgo
            }
            return weight;
        }

        bool IsRunning(string id)
        {
            foreach (var active in Active)
            {
                if (active.Def.id == id)
                    return true;
            }
            return false;
        }

        public void Fire(EventDef def)
        {
            var state = m_Sim.State;
            if (IsRunning(def.id) || def.kind == "storm" && CurrentStorm != null)
                return;
            m_Fired.Add(def.id);
            m_LastFired[def.id] = state.Time;
            m_LastEventTime = state.Time;
            m_PerSol[state.Sol]++;
            state.EventsHandled++;

            var active = new ActiveEvent { Def = def, Sol = state.Sol, Stage = def.warningSeconds > 0f ? EventStage.Warning : EventStage.Active, Timer = def.warningSeconds };
            Active.Add(active);
            if (def.kind == "storm")
            {
                CurrentStorm = active;
                active.Timer = m_Sim.Config.events.stormWarningSeconds;
                active.Stage = EventStage.Warning;
            }

            if (active.Stage == EventStage.Warning)
                m_Sim.Post(SimMessageKind.EventWarning, string.IsNullOrEmpty(def.warning) ? def.description : def.warning, def.id);
            else
                Activate(active);
        }

        void Advance(ActiveEvent active, float dt)
        {
            if (active.Stage == EventStage.Warning)
            {
                active.Timer -= dt;
                if (active.Timer <= 0f)
                    Activate(active);
                return;
            }

            if (active.Def.kind == "storm" && active.Stage == EventStage.Active)
            {
                active.Timer -= dt;
                if (active.Timer <= 0f)
                {
                    var state = m_Sim.State;
                    state.StormActive = false;
                    if (state.ShelterActive) state.StormsSheltered++;
                    else state.StormsUnsheltered++;
                    active.Stage = EventStage.Resolved;
                    CurrentStorm = null;
                    m_Sim.Post(SimMessageKind.StormEnded, "La actividad solar ha disminuido. Ya se pueden retomar las actividades normales.", active.Def.id);
                }
            }
        }

        void Activate(ActiveEvent active)
        {
            var state = m_Sim.State;
            var def = active.Def;
            active.Stage = EventStage.Active;
            switch (def.kind)
            {
                case "breakdown":
                    state[def.TargetSystem].Broken = true;
                    m_Sim.Post(SimMessageKind.EventStarted, def.description, def.id);
                    break;
                case "decision":
                    PendingDecision = active;
                    m_Sim.Post(SimMessageKind.DecisionRequired, def.description, def.id);
                    break;
                case "storm":
                    state.StormActive = true;
                    active.Timer = m_Sim.Config.events.stormDurationSeconds;
                    m_Sim.Post(SimMessageKind.StormStarted, def.description, def.id);
                    break;
            }
        }

        public void NotifyRepaired(SystemId id)
        {
            foreach (var active in Active)
            {
                if (active.Def.kind == "breakdown" && active.Def.TargetSystem == id && active.Stage != EventStage.Resolved)
                {
                    active.Stage = EventStage.Resolved;
                    m_Sim.Post(SimMessageKind.EventResolved, $"Emergencia resuelta: {active.Def.title}.", active.Def.id);
                }
            }
        }

        public void ResolveDecision(int optionIndex)
        {
            var active = PendingDecision;
            if (active == null)
                return;
            var state = m_Sim.State;
            var options = active.Def.options;
            var option = options.Length > 0 ? options[Math.Max(0, Math.Min(options.Length - 1, optionIndex))] : null;
            if (option != null)
            {
                foreach (var effect in option.effects)
                    state.Add(effect.stat, effect.delta);
                if (!string.IsNullOrEmpty(option.powerOff) && Enum.TryParse(option.powerOff, true, out SystemId off))
                    state[off].Powered = false;
                if (option.shelter)
                    state.ShelterActive = true;
                state.Decisions.Add($"Sol {state.Sol}: {active.Def.title} → {option.label}.");
            }
            active.Stage = EventStage.Resolved;
            PendingDecision = null;
            m_Sim.Post(SimMessageKind.EventResolved, option?.explanation ?? "Decisión tomada.", active.Def.id);
        }

        /// <summary>Lanza un evento por identificador (pruebas y depuración).</summary>
        public bool FireById(string id)
        {
            foreach (var def in m_Catalog.events)
            {
                if (def.id == id)
                {
                    Fire(def);
                    return true;
                }
            }
            return false;
        }
    }
}

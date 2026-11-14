using NUnit.Framework;
using Sol3.Game.Data;
using Sol3.Game.Sim;

namespace Sol3.Game.Tests
{
    /// <summary>Pruebas de la simulación (independiente de los gráficos) y de la carga de datos sin conexión.</summary>
    public sealed class SimulationTests
    {
        static ColonySimulation NewSim(int seed = 7, bool events = true)
        {
            GameData.Load();
            return new ColonySimulation(GameData.Config, events ? GameData.Events : new EventCatalog(), seed);
        }

        static void Run(ColonySimulation sim, float seconds, float step = 0.1f)
        {
            for (float t = 0f; t < seconds && !sim.Ended; t += step)
                sim.Tick(step);
        }

        [Test]
        public void OfflineData_LoadsFromResources()
        {
            GameData.Load();
            Assert.Greater(GameData.Config.solSeconds, 0f);
            Assert.AreEqual(3, GameData.Config.sols);
            Assert.Greater(GameData.Events.events.Length, 5, "events.json");
            Assert.Greater(GameData.Archive.cards.Length, 5, "science_archive.json");
            Assert.IsNotNull(GameData.Donki, "nasa_donki.json");
        }

        [Test]
        public void ScienceArchive_SeparatesRealAndSimulated()
        {
            GameData.Load();
            bool real = false, simulated = false;
            foreach (var card in GameData.Archive.cards)
            {
                Assert.That(card.category == "REAL" || card.category == "SIMULACIÓN", $"Categoría inválida en {card.id}");
                if (card.category == "REAL")
                {
                    real = true;
                    Assert.IsFalse(string.IsNullOrEmpty(card.source), $"La ficha real {card.id} necesita fuente");
                }
                else simulated = true;
            }
            Assert.IsTrue(real && simulated);
        }

        [Test]
        public void ScienceData_ContainsNoApiKey()
        {
            GameData.Load();
            var json = UnityEngine.JsonUtility.ToJson(GameData.Donki) + UnityEngine.JsonUtility.ToJson(GameData.Archive);
            StringAssert.DoesNotContain("api_key", json);
            StringAssert.DoesNotContain("NASA_API_KEY", json);
        }

        [Test]
        public void Resources_StayWithinLimits()
        {
            var sim = NewSim();
            sim.State.Add("energy", 500f);
            sim.State.Add("oxygen", -500f);
            Assert.AreEqual(100f, sim.State.Energy);
            Assert.AreEqual(0f, sim.State.Oxygen);
            Run(sim, 60f);
            foreach (var stat in new[] { "energy", "oxygen", "water", "food", "health", "radiation" })
            {
                float v = sim.State.Get(stat);
                Assert.That(v >= 0f && v <= 100f, $"{stat} fuera de rango: {v}");
            }
        }

        [Test]
        public void ZeroDeltaTime_DoesNotAdvance()
        {
            var sim = NewSim();
            var before = sim.State.Energy;
            sim.Tick(0f);
            Assert.AreEqual(0f, sim.State.Time);
            Assert.AreEqual(before, sim.State.Energy);
        }

        [Test]
        public void Night_HasNoSolarProduction_AndDrainsBattery()
        {
            var sim = NewSim(events: false);
            float dayLength = sim.Config.solSeconds * sim.Config.dayFraction;
            Run(sim, dayLength + 2f);
            Assert.AreEqual(DayPhase.Night, sim.State.Phase);
            Assert.AreEqual(0f, sim.Production());
            float before = sim.State.Energy;
            Run(sim, 10f);
            Assert.Less(sim.State.Energy, before);
        }

        [Test]
        public void TurningSystemsOff_ReducesLoad()
        {
            var sim = NewSim(events: false);
            float load = sim.Load();
            sim.SetPower(SystemId.Greenhouse, false);
            sim.SetPower(SystemId.Recycler, false);
            Assert.Less(sim.Load(), load);
        }

        [Test]
        public void BrokenLifeSupport_LowersOxygen_AndRepairFixesIt()
        {
            var sim = NewSim(events: false);
            sim.State[SystemId.LifeSupport].Broken = true;
            float before = sim.State.Oxygen;
            Run(sim, 10f);
            Assert.Less(sim.State.Oxygen, before);
            int parts = sim.State.SpareParts;
            sim.CompleteRepair(SystemId.LifeSupport);
            Assert.IsFalse(sim.State[SystemId.LifeSupport].Broken);
            Assert.AreEqual(parts - 1, sim.State.SpareParts);
            Assert.AreEqual(1f, sim.State[SystemId.LifeSupport].Integrity);
        }

        [Test]
        public void RepairWithoutParts_LeavesLowerIntegrity()
        {
            var sim = NewSim(events: false);
            sim.State.SpareParts = 0;
            sim.State[SystemId.Recycler].Broken = true;
            sim.CompleteRepair(SystemId.Recycler);
            Assert.AreEqual(0.75f, sim.State[SystemId.Recycler].Integrity, 0.001f);
        }

        [Test]
        public void TutorialFault_FiresOnSol1()
        {
            var sim = NewSim();
            Run(sim, sim.Config.events.tutorialFaultAt + 3f);
            bool started = sim.Messages.Exists(m => m.Kind == SimMessageKind.EventStarted || m.Kind == SimMessageKind.EventWarning);
            Assert.IsTrue(started, "La avería guiada del Sol 1 debe aparecer.");
        }

        [Test]
        public void Storm_WarnsBeforeActivating_AndShelterProtects()
        {
            var unsheltered = NewSim(events: true);
            var sheltered = NewSim(events: true);
            Assert.IsTrue(unsheltered.Events.FireById("solar_activity"));
            Assert.IsTrue(sheltered.Events.FireById("solar_activity"));
            Assert.IsFalse(unsheltered.State.StormActive, "Primero debe haber un aviso.");
            sheltered.SetShelter(true);
            float total = unsheltered.Config.events.stormWarningSeconds + unsheltered.Config.events.stormDurationSeconds * 0.5f;
            for (float t = 0; t < total; t += 0.1f)
            {
                unsheltered.Tick(0.1f);
                sheltered.Tick(0.1f);
            }
            Assert.IsTrue(unsheltered.State.StormActive);
            Assert.Greater(unsheltered.State.Radiation, sheltered.State.Radiation + 10f);
        }

        [Test]
        public void Decision_AppliesChosenOption()
        {
            var sim = NewSim();
            Assert.IsTrue(sim.Events.FireById("energy_shortage"));
            Run(sim, 30f);
            Assert.IsNotNull(sim.Events.PendingDecision);
            sim.Events.ResolveDecision(2);
            Assert.IsNull(sim.Events.PendingDecision);
            Assert.IsFalse(sim.State[SystemId.Lab].Powered);
        }

        [Test]
        public void Research_And_Report_LeadToSuccess()
        {
            var sim = NewSim(events: false);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(sim.StartExperiment(i));
                sim.State.ExperimentProgress = 1f;
                Assert.IsTrue(sim.CollectExperiment());
            }
            Assert.IsFalse(sim.SendReport(), "Sin antena alineada no se puede enviar.");
            sim.State.AntennaAligned = true;
            Assert.IsTrue(sim.SendReport());
            sim.State.Energy = 100f;
            sim.State.Food = 100f;
            sim.State.Water = 100f;
            Run(sim, sim.Config.rates.transmissionSeconds + 1f);
            Assert.AreEqual(ReportStatus.Delivered, sim.State.Report);
            sim.State.Time = sim.MissionSeconds - 0.05f;
            sim.Tick(0.1f);
            Assert.AreEqual(MissionOutcome.Success, sim.State.Outcome);
        }

        [Test]
        public void SurvivingWithoutResearch_IsPartial()
        {
            var sim = NewSim(events: false);
            sim.State.Time = sim.MissionSeconds - 0.05f;
            sim.Tick(0.1f);
            Assert.AreEqual(MissionOutcome.Partial, sim.State.Outcome);
        }

        [Test]
        public void ZeroHealth_IsFailure_AndEndsSimulation()
        {
            var sim = NewSim(events: false);
            sim.State.Health = 0.01f;
            sim.State.Oxygen = 0f;
            sim.State[SystemId.LifeSupport].Broken = true;
            Run(sim, 5f);
            Assert.AreEqual(MissionOutcome.Failure, sim.State.Outcome);
            float time = sim.State.Time;
            sim.Tick(1f);
            Assert.AreEqual(time, sim.State.Time, "Tras el final la simulación no avanza.");
        }

        [Test]
        public void Restart_CreatesFreshState()
        {
            var a = NewSim(3);
            Run(a, 50f);
            var b = NewSim(3);
            Assert.AreEqual(0f, b.State.Time);
            Assert.AreEqual(1, b.State.Sol);
            Assert.AreEqual(GameData.Config.start.energy, b.State.Energy);
        }

        [Test]
        public void SameSeed_IsDeterministic()
        {
            var a = NewSim(11);
            var b = NewSim(11);
            Run(a, 120f);
            Run(b, 120f);
            Assert.AreEqual(a.State.Energy, b.State.Energy);
            Assert.AreEqual(a.State.EventsHandled, b.State.EventsHandled);
        }

        [Test]
        public void Score_IsHigherForSuccess()
        {
            var good = NewSim(events: false);
            good.State.Research = 3;
            good.State.Report = ReportStatus.Delivered;
            good.State.Time = good.MissionSeconds - 0.05f;
            good.Tick(0.1f);
            var bad = NewSim(events: false);
            bad.State.Time = bad.Config.solSeconds;
            Assert.Greater(Progress.Score(good), Progress.Score(bad));
        }
    }
}

using System;
using System.Collections.Generic;
using Sol3.Game.Sim;
using UnityEngine;

namespace Sol3.Game.Data
{
    /// <summary>Ficha del archivo científico. category = "REAL" (fuente verificable) o "SIMULACIÓN" (modelo del juego).</summary>
    [Serializable]
    public sealed class ScienceCard
    {
        public string id;
        public string category;
        public string title;
        public string summary;
        public string usedInGame;
        public string source;
        public string url;
    }

    [Serializable]
    public sealed class ScienceArchive
    {
        public string disclaimer;
        public ScienceCard[] cards = Array.Empty<ScienceCard>();
    }

    [Serializable]
    public sealed class DonkiRecord
    {
        public string type;      // FLR | CME | GST
        public string id;
        public string time;
        public string detail;
        public string link;
    }

    /// <summary>Registros históricos de DONKI descargados con scripts/fetch-nasa-data.mjs (no son datos en tiempo real).</summary>
    [Serializable]
    public sealed class DonkiData
    {
        public string status = "not_downloaded";
        public string source;
        public string queriedAt;
        public string startDate;
        public string endDate;
        public string note;
        public DonkiRecord[] records = Array.Empty<DonkiRecord>();

        public bool HasRecords => records != null && records.Length > 0;
    }

    public sealed class Experiment
    {
        public string Title;
        public string Description;
        public string Science;
    }

    public static class GameData
    {
        public static SimConfig Config { get; private set; }
        public static EventCatalog Events { get; private set; }
        public static ScienceArchive Archive { get; private set; }
        public static DonkiData Donki { get; private set; }

        public static readonly Experiment[] Experiments =
        {
            new Experiment
            {
                Title = "Análisis del regolito",
                Description = "Analiza una muestra de suelo de los alrededores de la base para saber si se podría usar en cultivos.",
                Science = "Ciencia real: la sonda Phoenix (2008) detectó percloratos en el suelo marciano, sales que habría que eliminar antes de cultivar en él.",
            },
            new Experiment
            {
                Title = "Lechugas con luz LED",
                Description = "Compara el crecimiento de brotes bajo distintas mezclas de luz LED del invernadero.",
                Science = "Ciencia real: en la Estación Espacial Internacional se han cultivado lechugas con el sistema Veggie y luces LED.",
            },
            new Experiment
            {
                Title = "Dosimetría de la base",
                Description = "Mide cómo varía la radiación entre las salas y el refugio para mejorar los protocolos.",
                Science = "Ciencia real: el instrumento RAD del rover Curiosity mide la radiación en la superficie de Marte. Los valores de este juego son simulados.",
            },
            new Experiment
            {
                Title = "Microbios en el agua reciclada",
                Description = "Vigila los microorganismos del circuito cerrado de agua de la colonia.",
                Science = "Ciencia real: los sistemas de soporte vital de la Estación Espacial recuperan agua de la humedad del aire y de la orina, y se controla su calidad.",
            },
        };

        public static void Load()
        {
            Config = LoadJson<SimConfig>("Data/sim_config") ?? new SimConfig();
            Events = LoadJson<EventCatalog>("Data/events") ?? new EventCatalog();
            Archive = LoadJson<ScienceArchive>("Data/science_archive") ?? new ScienceArchive();
            Donki = LoadJson<DonkiData>("Data/nasa_donki") ?? new DonkiData();
        }

        static T LoadJson<T>(string path) where T : class
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[SOL-3] No se encontró Resources/{path}.json; se usan valores por defecto.");
                return null;
            }
            try
            {
                return JsonUtility.FromJson<T>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SOL-3] Error leyendo {path}.json: {e.Message}");
                return null;
            }
        }

        /// <summary>Registro DONKI histórico que sirve de "inspiración real" para un evento solar del juego.</summary>
        public static DonkiRecord PickSolarRecord(int seed)
        {
            if (Donki == null || !Donki.HasRecords)
                return null;
            var list = new List<DonkiRecord>();
            foreach (var r in Donki.records)
                if (r.type == "FLR" || r.type == "CME")
                    list.Add(r);
            if (list.Count == 0)
                list.AddRange(Donki.records);
            return list[Math.Abs(seed) % list.Count];
        }
    }

    /// <summary>Puntuación, logros y récords (guardados con PlayerPrefs; en WebGL se guardan en el almacenamiento local del navegador).</summary>
    public static class Progress
    {
        public sealed class Achievement
        {
            public string Id, Title, Description;
            public Func<ColonyState, ColonySimulation, bool> Test;
        }

        public static readonly Achievement[] Achievements =
        {
            new Achievement { Id = "mission", Title = "Misión cumplida", Description = "Completa los tres soles con éxito.", Test = (s, sim) => s.Outcome == MissionOutcome.Success },
            new Achievement { Id = "engineer", Title = "Ingeniería de campo", Description = "Realiza 5 reparaciones.", Test = (s, sim) => s.Repairs >= 5 },
            new Achievement { Id = "farmer", Title = "Agricultura marciana", Description = "Cosecha 3 veces.", Test = (s, sim) => s.Harvests >= 3 },
            new Achievement { Id = "shield", Title = "Escudo solar", Description = "Protege a la tripulación en todas las tormentas.", Test = (s, sim) => s.StormsSheltered > 0 && s.StormsUnsheltered == 0 },
            new Achievement { Id = "nolights", Title = "Luces encendidas", Description = "Termina sin ningún apagón.", Test = (s, sim) => s.Blackouts == 0 && s.Outcome != MissionOutcome.Failure },
            new Achievement { Id = "science", Title = "Ciencia completa", Description = "Completa las 4 investigaciones.", Test = (s, sim) => s.Research >= 4 },
            new Achievement { Id = "messenger", Title = "Enlace con la Tierra", Description = "La Tierra recibe tu informe científico.", Test = (s, sim) => s.Report == ReportStatus.Delivered },
        };

        public static int Score(ColonySimulation sim)
        {
            var s = sim.State;
            int score = 0;
            score += Mathf.RoundToInt(Mathf.Min(s.Time, sim.MissionSeconds) / sim.Config.solSeconds * 1000f);
            score += s.Research * 500;
            score += s.Report == ReportStatus.Delivered ? 1500 : 0;
            score += s.Repairs * 120 + s.Harvests * 150 + s.StormsSheltered * 300 + s.EventsHandled * 80;
            score -= s.Blackouts * 250 + s.StormsUnsheltered * 400 + s.FailedRepairs * 40;
            score += Mathf.RoundToInt(s.Health * 10f);
            if (s.Outcome == MissionOutcome.Success) score += 2000;
            return Mathf.Max(0, score);
        }

        public static bool IsUnlocked(string id) => PlayerPrefs.GetInt("SOL3.Ach." + id, 0) == 1;

        /// <summary>Registra el resultado: devuelve los logros nuevos y si es récord.</summary>
        public static List<Achievement> Record(ColonySimulation sim, int score, out bool newBest)
        {
            var unlocked = new List<Achievement>();
            foreach (var a in Achievements)
            {
                if (!IsUnlocked(a.Id) && a.Test(sim.State, sim))
                {
                    PlayerPrefs.SetInt("SOL3.Ach." + a.Id, 1);
                    unlocked.Add(a);
                }
            }
            int best = PlayerPrefs.GetInt("SOL3.Best", 0);
            newBest = score > best;
            if (newBest)
                PlayerPrefs.SetInt("SOL3.Best", score);
            PlayerPrefs.Save();
            return unlocked;
        }

        public static int Best => PlayerPrefs.GetInt("SOL3.Best", 0);
    }
}

using System.Collections.Generic;

namespace Sol3.Game.Sim
{
    public readonly struct Objective
    {
        public readonly string Text;
        public readonly bool Done;
        public readonly string Where;

        public Objective(string text, bool done, string where)
        {
            Text = text;
            Done = done;
            Where = where;
        }
    }

    /// <summary>Objetivos diarios: Sol 1 aprendizaje, Sol 2 decisiones, Sol 3 desafío final.</summary>
    public static class Objectives
    {
        public static List<Objective> For(ColonySimulation sim)
        {
            var s = sim.State;
            int goal = sim.Config.goals.research;
            bool anyCrop = s.Harvests > 0;
            foreach (var crop in s.Crops)
                anyCrop |= crop.Planted;

            var list = new List<Objective>();
            switch (s.Sol)
            {
                case 1:
                    list.Add(new Objective("Revisa el estado de la colonia", s.VisitedControl, "Centro de control"));
                    list.Add(new Objective("Repara la avería de la sala de máquinas", s.Repairs > 0, "Sala de máquinas"));
                    list.Add(new Objective("Planta un cultivo", anyCrop, "Invernadero"));
                    list.Add(new Objective("Empieza la noche con la batería sobre 60 %", s.EnergyAtNight[1] >= 60f, "Centro de control"));
                    break;
                case 2:
                    list.Add(new Objective($"Completa 2 investigaciones ({s.Research}/2)", s.Research >= 2, "Laboratorio"));
                    list.Add(new Objective("Protege a la tripulación de la actividad solar", s.StormsSheltered > 0, "Refugio"));
                    list.Add(new Objective("Cosecha alimentos", s.Harvests > 0, "Invernadero"));
                    list.Add(new Objective("Empieza la noche con la batería sobre 50 %", s.EnergyAtNight[2] >= 50f, "Centro de control"));
                    break;
                default:
                    list.Add(new Objective($"Completa la investigación ({s.Research}/{goal})", s.Research >= goal, "Laboratorio"));
                    list.Add(new Objective("Alinea la antena y envía el informe", s.Report == ReportStatus.Delivered, "Centro de control"));
                    list.Add(new Objective("Mantén a salvo a la tripulación hasta el final", false, "Toda la base"));
                    break;
            }
            return list;
        }
    }
}

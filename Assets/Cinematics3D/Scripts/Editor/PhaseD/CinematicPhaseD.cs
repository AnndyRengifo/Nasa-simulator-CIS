using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Fase D — Cinemáticas de final: ending_success, ending_failure y ending_partial.
    /// Son solo presentación: la lógica del juego decide qué final corresponde y después lo pide.
    /// Cada final se genera en su escena CIN_&lt;id&gt; con Timeline, cámaras, acciones, ambientes y
    /// subtítulos, y queda registrado en el catálogo y en la build. Volver a ejecutarla regenera
    /// solo estas tres escenas.
    /// </summary>
    internal static class CinematicPhaseD
    {
        const string Title = "SOL-3 · Cinemáticas · Fase D";

        static readonly Vector3 Control = new Vector3(6000f, 0f, 0f);
        static readonly Vector3 Power = new Vector3(8000f, 0f, 0f);
        static readonly Vector3 Shelter = new Vector3(10000f, 0f, 0f);
        static readonly Vector3 Earth = new Vector3(12000f, 0f, 0f);

        static readonly Color DayFog = new Color(0.74f, 0.50f, 0.34f);
        static readonly Color DaySky = new Color(0.62f, 0.47f, 0.39f), DayEquator = new Color(0.45f, 0.31f, 0.24f), DayGround = new Color(0.20f, 0.13f, 0.10f);
        static readonly Color GoldenFog = new Color(0.82f, 0.56f, 0.36f);
        static readonly Color GoldenSky = new Color(0.70f, 0.52f, 0.40f), GoldenEquator = new Color(0.52f, 0.36f, 0.26f), GoldenGround = new Color(0.22f, 0.14f, 0.10f);
        static readonly Color DuskFog = new Color(0.36f, 0.34f, 0.42f);
        static readonly Color DuskSky = new Color(0.36f, 0.38f, 0.50f), DuskEquator = new Color(0.30f, 0.25f, 0.25f), DuskGround = new Color(0.12f, 0.09f, 0.08f);
        static readonly Color CrisisFog = new Color(0.30f, 0.14f, 0.12f);
        static readonly Color CrisisSky = new Color(0.26f, 0.16f, 0.16f), CrisisEquator = new Color(0.18f, 0.10f, 0.10f), CrisisGround = new Color(0.06f, 0.04f, 0.04f);

        const string AURA = "AURA";
        const string CT = "CT";

        public static void RunInteractive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog(Title, "Sal del modo Play y espera a que Unity termine de compilar o importar.", "Entendido");
                return;
            }

            if (!EditorUtility.DisplayDialog(Title,
                    "Se generarán (o regenerarán) los tres finales:\n\n" +
                    "• CIN_ending_success (≈52 s)\n• CIN_ending_failure (≈45 s)\n• CIN_ending_partial (≈39 s)\n\n" +
                    "Cada uno con su Timeline, cámaras, animaciones, ambientes y subtítulos, registrado en el catálogo y en la build. " +
                    "Al terminar se abrirá CIN_ending_success.", "Generar", "Cancelar"))
                return;

            var report = Run();
            string log = report.Publish("Fase D", "SOL3_Cinematics_PhaseD.txt");
            var validation = CinematicValidator.Validate();
            string validationLog = validation.Publish("Validación del sistema de cinemáticas", "SOL3_Cinematics_Validation.txt");
            EditorUtility.DisplayDialog(Title,
                $"Fase D: {report.Summary}\nValidación: {validation.Summary}\n\n" +
                "Abre cualquier escena CIN_ending_* de Assets/Cinematics3D/Scenes y pulsa Play para verla con subtítulos.\n\n" +
                $"Informes en la Consola y en:\n{log}\n{validationLog}", "Cerrar");
        }

        public static CinematicReport Run()
        {
            var report = new CinematicReport();
#if !(SOL3_CINEMACHINE && SOL3_SPLINES)
            report.Error("Fase D", "Cinemachine o Splines no están instalados.");
            return report;
#else
            if (!CinematicPhaseB.IsPhaseAReady())
            {
                report.Error("Fase D", "La Fase A no está configurada. Ejecuta antes 'Fase B · Crear recursos y galería'.");
                return report;
            }
            foreach (var path in CinematicPhaseB.ExpectedPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    report.Error("Fase D", $"Falta {path}. Ejecuta antes 'Fase B · Crear recursos y galería'.");
                    return report;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.Warning("Fase D", "Cancelado: hay escenas con cambios sin guardar.");
                return report;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            int rendererIndex = catalog.Globals.cinematicRendererIndex;
            var ctx = new BuildContext { Overwrite = false, Layer = CinematicLayers.Index, Report = report };

            Step(ctx, CinematicIds.EndingSuccess, 0.1f, () => Success(ctx, rendererIndex));
            Step(ctx, CinematicIds.EndingFailure, 0.4f, () => Failure(ctx, rendererIndex));
            Step(ctx, CinematicIds.EndingPartial, 0.7f, () => Partial(ctx, rendererIndex));
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();

            string first = CinematicEditorPaths.SceneFor(CinematicIds.EndingSuccess);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(first) != null)
                EditorSceneManager.OpenScene(first, OpenSceneMode.Single);
            report.Info("Fase D", "Los finales solo presentan el resultado: nunca deciden ni modifican la victoria, la derrota o el final parcial.");
            return report;
#endif
        }

        static void Step(BuildContext ctx, string id, float progress, Action build)
        {
            EditorUtility.DisplayProgressBar(Title, $"Generando {id}…", progress);
            try
            {
                build();
            }
            catch (Exception exception)
            {
                ctx.Report.Error($"Cinemática '{id}'", "Error al generar: " + exception.Message);
                Debug.LogException(exception);
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }
        }

#if SOL3_CINEMACHINE && SOL3_SPLINES
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static GameObject Child(GameObject parent, string path)
        {
            var child = parent != null ? parent.transform.Find(path) : null;
            return child != null ? child.gameObject : null;
        }

        static GameObject Screen(CinematicSceneBuilder b, string material, Vector3 position, Vector3 euler, Vector3 scale, string name) =>
            CinematicBuildKit.Prim(PrimitiveType.Quad, name, b.Environment, b.Ctx.Mat(material), b.Ctx.Layer, position, euler, scale, false);

        // ================================================================== VICTORIA

        static void Success(BuildContext ctx, int renderer)
        {
            const double end = 52.0;
            var b = new CinematicSceneBuilder(ctx, CinematicIds.EndingSuccess, renderer);
            b.Place("PF_Set_MarsSurface", b.Environment, Vector3.zero, Vector3.zero);
            b.Place("PF_FX_MarsDust", b.Fx, V(0f, 3f, 0f), Vector3.zero);
            b.Place("PF_Room_ControlCenter", b.Environment, Control, Vector3.zero);
            b.Place("PF_Room_EarthControl", b.Environment, Earth, Vector3.zero);
            var astronaut = b.Place("PF_Astronaut", b.Actors, Control + V(4.2f, 0f, -1.4f), V(0f, 90f, 0f));
            var aura = b.Place("PF_AURA", b.Actors, Control + V(0f, 1.75f, -1.2f), Vector3.zero);
            var pulse = b.Place("PF_FX_TransmissionPulse", b.Fx, V(8f, 10.5f, 15.8f), Vector3.zero);

            // 1. Centro de control.
            var wide = b.Dolly("01_Control", new[] { Control + V(-4.8f, 2.3f, -3.8f), Control + V(-3.2f, 2f, -3.2f) },
                b.Look("Look_Control", Control + V(2f, 1.3f, -0.6f)), 52f, 0.0, 7.0);
            b.Shot(wide, 0.0, 7.0);

            // 2. Preparación del informe científico.
            var report = b.Cam("02_Report", Control + V(3.1f, 1.75f, -2.7f), b.Look("Look_Report", Control + V(5.05f, 1.35f, -1.4f)), 40f, 0.2f);
            b.Shot(report, 7.0, 7.0);
            b.Clips(b.Track(astronaut, CinematicBindingKeys.Astronaut), 0.0, ("AC_Astronaut_WorkConsole", 14.5), ("AC_Astronaut_CheckWrist", 3.0),
                ("AC_Astronaut_Idle", 13.5), ("AC_Astronaut_Celebrate", 2.5), ("AC_Astronaut_Idle", end - 31.4));

            // 3. Transmisión hacia la Tierra.
            var transmission = b.Dolly("03_Transmission", new[] { V(20f, 4f, 30f), V(16f, 8f, 28f) }, b.Look("Look_Dish", V(8f, 9.5f, 15f)), 45f, 14.0, 8.0);
            b.Shot(transmission, 14.0, 8.0);
            b.Active(pulse, "TransmissionPulse", (14.0, 9.0));

            // 4. Confirmación de recepción en Control Tierra.
            var earth = b.Dolly("04_EarthControl", new[] { Earth + V(-2f, 2.6f, -5.4f), Earth + V(0f, 2.3f, -4.2f) },
                b.Look("Look_Earth", Earth + V(0f, 2.4f, 5f)), 50f, 22.0, 8.5);
            b.Shot(earth, 22.0, 8.5);

            // 5. El astronauta y AURA.
            b.Active(aura, AURA, (29.5, end - 29.5));
            b.Clips(b.Track(aura, AURA), 29.5, ("AC_AURA_Appear", 2.3), ("AC_AURA_Speak", end - 31.5));
            var duo = b.Cam("05_Duo", Control + V(-1.6f, 1.6f, -3.9f), b.Look("Look_Duo", Control + V(1.9f, 1.5f, -1.3f)), 44f, 0.2f);
            b.Shot(duo, 30.5, 11.5);

            // 6. Vista exterior final de Base Aurora.
            b.Active(b.Ambience("Env_Golden", "M_Sky_MarsDay", true, GoldenFog, 0.005f, GoldenSky, GoldenEquator, GoldenGround), "Env_Golden", (42.0, end - 42.0));
            var outro = b.Dolly("06_Outro", new[] { V(-20f, 6f, -30f), V(-45f, 18f, -62f), V(-70f, 34f, -95f) },
                b.Look("Look_Outro", V(4f, 3f, 0f)), 48f, 42.0, end - 42.0);
            b.Shot(outro, 42.0, end - 42.0);

            b.Line(AURA, "Transmisión completada.", 15.5f, 18f);
            b.Line(AURA, "Nuestros resultados han sido enviados a la Tierra.", 18.5f, 22.5f);
            b.Line(CT, "Base Aurora, hemos recibido su informe.", 23f, 27f);
            b.Line(CT, "Excelente trabajo, tripulación.", 27.5f, 30.5f);
            b.Line(AURA, "Superamos la misión gracias a nuestras decisiones.", 32f, 36f);
            b.Line(AURA, "Cada recurso que protegimos nos permitió llegar hasta aquí.", 36.5f, 41.5f);

            b.Finish("M_Sky_MarsDay", DayFog, 0.006f, DaySky, DayEquator, DayGround);
        }

        // ================================================================== DERROTA

        static void Failure(BuildContext ctx, int renderer)
        {
            const double end = 45.0;
            var b = new CinematicSceneBuilder(ctx, CinematicIds.EndingFailure, renderer);
            var set = b.Place("PF_Set_MarsSurface", b.Environment, Vector3.zero, Vector3.zero);
            b.Place("PF_FX_MarsDust", b.Fx, V(0f, 3f, 0f), Vector3.zero);
            b.Place("PF_FX_MarsDust", b.Fx, V(10f, 5f, -10f), V(0f, 40f, 0f), "PF_FX_MarsDust_Storm");
            var control = b.Place("PF_Room_ControlCenter", b.Environment, Control, Vector3.zero);
            var power = b.Place("PF_Room_PowerRoom", b.Environment, Power, Vector3.zero);
            b.Place("PF_Room_Shelter", b.Environment, Shelter, Vector3.zero);
            var astronaut = b.Place("PF_Astronaut", b.Actors, Control + V(0f, 0f, 1.9f), Vector3.zero);
            var aura = b.Place("PF_AURA", b.Actors, Control + V(0f, 1.75f, -1.2f), Vector3.zero);
            var sheltered = b.Place("PF_Astronaut", b.Actors, Shelter + V(0.3f, 0f, 0.2f), V(0f, 160f, 0f), "PF_Astronaut_Shelter");
            if (sheltered != null)
                b.Key(sheltered, "Astronaut_Shelter");
            var sparks = b.Place("PF_FX_Sparks", b.Fx, Power + V(2.1f, 2.6f, -1.5f), V(-30f, -60f, 0f));
            var sparksRack = b.Place("PF_FX_Sparks", b.Fx, Power + V(-0.9f, 1.8f, 3.2f), V(-20f, 180f, 0f), "PF_FX_Sparks_Rack");

            // Base en estado crítico: balizas rojas y luz de emergencia exterior.
            var exteriorAlarm = CinematicBuildKit.Obj("ExteriorAlarm", b.Environment, ctx.Layer);
            CinematicBuildKit.AddPointLight(exteriorAlarm.transform, "AlarmLight", ctx.Layer, V(0f, 7f, 0f), CinematicBuildKit.Hex("#DB6262"), 4f, 30f);
            b.Place("PF_Prop_AlertBeacon", exteriorAlarm.transform, V(0f, 5.6f, 0f), Vector3.zero);
            b.Place("PF_Prop_AlertBeacon", exteriorAlarm.transform, V(24f, 3.05f, 6f), Vector3.zero);

            // 1-3. Alerta, luces de emergencia y AURA en modo alerta durante toda la secuencia.
            var alertScreen = Screen(b, "M_Screen_Alert", Control + V(-5.98f, 2.3f, 0f), V(0f, -90f, 0f), V(3f, 1.5f, 1f), "AlertScreen");
            b.Active(alertScreen, "AlertScreen", (0.0, end));
            b.Active(Child(control, "EmergencyLights"), "EmergencyLights", (0.0, end));
            b.Active(Child(control, "Lights_Normal"), "Lights_Normal", (0.0, 0.5));
            b.Clips(b.Track(astronaut, CinematicBindingKeys.Astronaut), 0.0, ("AC_Astronaut_WorkConsole", 3.5), ("AC_Astronaut_Concern", end - 3.2));
            var alert = b.Dolly("01_Alert", new[] { Control + V(-4.5f, 2.2f, -3.8f), Control + V(-2.6f, 1.9f, -2f) },
                b.Look("Look_Alert", Control + V(0f, 1.4f, 1.8f)), 50f, 0.0, 7.0);
            b.Shot(alert, 0.0, 7.0);
            b.Active(aura, AURA, (3.0, end - 3.0));
            b.Clips(b.Track(aura, AURA), 3.0, ("AC_AURA_Appear", 2.3), ("AC_AURA_Alert", end - 5.0));

            // 2. Los sistemas esenciales fallan.
            b.Active(Child(power, "EmergencyLights"), "PowerEmergencyLights", (7.0, end - 7.0));
            b.Active(Child(power, "Lights_Normal"), "PowerLights_Normal", (0.0, 8.5));
            b.Active(sparks, "Sparks", (7.0, end - 7.0));
            b.Active(sparksRack, "Sparks_Rack", (9.0, end - 9.0));
            b.Clips(b.Track(Child(power, "BatteryBank"), "BatteryBank", true), 7.0, ("AC_Battery_Drain", end - 7.0));
            var systems = b.Dolly("02_SystemsFailing", new[] { Power + V(3.6f, 2.2f, -3.4f), Power + V(1.8f, 1.8f, -2.4f) },
                b.Look("Look_Systems", Power + V(-0.8f, 1.5f, 2.6f)), 50f, 7.0, 7.0);
            b.Shot(systems, 7.0, 7.0);

            // 4. Interrupción de las operaciones exteriores.
            b.SunMotion(0.0, end, new[]
            {
                (0f, 8f, 0.7f, new Color(0.95f, 0.6f, 0.45f)),
                (20f, 3f, 0.3f, new Color(0.8f, 0.45f, 0.4f)),
            });
            b.Clips(b.Track(Child(set, "BaseAurora/SolarArray"), "SolarArray", true), 14.0, ("AC_SolarArray_Stow", end - 14.0));
            b.Active(Child(set, "BaseAurora/SolarArray/OutputLight"), "SolarOutputLight", (0.0, 15.0));
            var outside = b.Cam("03_Interrupted", V(2f, 2.4f, -34f), b.Look("Look_Panels", V(12f, 1.4f, -25f)), 42f, 0.35f);
            b.Shot(outside, 14.0, 7.0);

            // 3. AURA informa.
            var auraClose = b.Cam("04_AuraAlert", Control + V(-1.8f, 1.6f, -3.4f), b.Look("Look_Aura", Control + V(0f, 1.75f, -1.2f)), 38f, 0.3f);
            b.Shot(auraClose, 21.0, 9.0);

            // 5. La tripulación se protege en el refugio.
            var shelter = b.Cam("05_Shelter", Shelter + V(-2.6f, 1.7f, -2.4f), b.Look("Look_Shelter", Shelter + V(0.4f, 1.1f, 1f)), 55f, 0.3f);
            b.Shot(shelter, 30.0, 7.0);
            b.Clips(b.Track(sheltered, "Astronaut_Shelter"), 30.0, ("AC_Astronaut_Idle", 1.5), ("AC_Astronaut_Concern", end - 31.2));

            // 6. Imagen final de la base en estado crítico.
            b.Active(b.Ambience("Env_Crisis", "M_Sky_MarsDusk", true, CrisisFog, 0.012f, CrisisSky, CrisisEquator, CrisisGround), "Env_Crisis", (37.0, end - 37.0));
            var final = b.Dolly("06_CriticalBase", new[] { V(-28f, 7f, -30f), V(-48f, 16f, -58f) }, b.Look("Look_Base", V(2f, 3f, 0f)), 48f, 37.0, end - 37.0);
            b.Shot(final, 37.0, end - 37.0);

            b.Line(AURA, "Alerta. Los sistemas esenciales han alcanzado un estado crítico.", 1f, 6f);
            b.Line(AURA, "No podemos mantener las operaciones actuales.", 8f, 12f);
            b.Line(AURA, "La misión científica ha sido interrumpida.", 15f, 19f);
            b.Line(AURA, "Debemos priorizar la seguridad de la tripulación.", 22f, 26.5f);
            b.Line(AURA, "En el espacio, incluso una decisión pequeña puede cambiar el resultado de toda una misión.", 30.5f, 37.5f);

            b.Finish("M_Sky_MarsDusk", DuskFog, 0.008f, DuskSky, DuskEquator, DuskGround);
        }

        // ================================================================== FINAL PARCIAL

        static void Partial(BuildContext ctx, int renderer)
        {
            const double end = 39.0;
            var b = new CinematicSceneBuilder(ctx, CinematicIds.EndingPartial, renderer);
            b.Place("PF_Set_MarsSurface", b.Environment, Vector3.zero, Vector3.zero);
            b.Place("PF_FX_MarsDust", b.Fx, V(0f, 3f, 0f), Vector3.zero);
            b.Place("PF_Room_ControlCenter", b.Environment, Control, Vector3.zero);
            var power = b.Place("PF_Room_PowerRoom", b.Environment, Power, Vector3.zero);
            var astronaut = b.Place("PF_Astronaut", b.Actors, Control + V(1.7f, 0f, -1.2f), V(0f, -90f, 0f));
            var aura = b.Place("PF_AURA", b.Actors, Control + V(0f, 1.75f, -1.2f), Vector3.zero);

            // Experimentos sin terminar: muestras pendientes y pantalla de datos incompletos.
            Screen(b, "M_Screen_Alert", Control + V(-5.98f, 2.3f, 0f), V(0f, -90f, 0f), V(2.2f, 1.1f, 1f), "IncompleteResults");
            b.Place("PF_Prop_Crate", b.Environment, Control + V(-4.6f, 0f, -2.2f), V(0f, 20f, 0f), "Samples_1");
            b.Place("PF_Prop_Crate", b.Environment, Control + V(-4.4f, 0.7f, -2.1f), V(0f, -10f, 0f), "Samples_2");

            // 1. El astronauta en la base.
            var astronautShot = b.Cam("01_Astronaut", Control + V(-0.6f, 1.7f, -2.6f), b.Look("Look_Astronaut", Control + V(1.7f, 1.35f, -1.2f)), 40f, 0.25f);
            b.Shot(astronautShot, 0.0, 8.0);
            b.Clips(b.Track(astronaut, CinematicBindingKeys.Astronaut), 0.0, ("AC_Astronaut_Idle", 3.0), ("AC_Astronaut_Concern", 5.5),
                ("AC_Astronaut_LookAround", 5.0), ("AC_Astronaut_Idle", 12.0), ("AC_Astronaut_Gesture", 3.0), ("AC_Astronaut_Idle", 12.0));

            // 2. Experimentos incompletos.
            var experiments = b.Cam("02_Experiments", Control + V(-2.6f, 1.6f, -0.4f), b.Look("Look_Experiments", Control + V(-5.4f, 1.4f, -1.2f)), 45f, 0.25f);
            b.Shot(experiments, 8.0, 7.0);

            // 3. Los sistemas esenciales siguen funcionando.
            var systems = b.Cam("03_Systems", Power + V(3f, 2f, -3f), b.Look("Look_Systems", Power + V(-1.5f, 1.3f, 3.2f)), 50f, 0.25f);
            b.Shot(systems, 15.0, 7.0);
            b.Clips(b.Track(Child(power, "BatteryBank"), "BatteryBank", true), 15.0, ("AC_Battery_Charge", end - 15.0));

            // 4. AURA presenta el resultado.
            b.Active(aura, AURA, (21.5, end - 21.5));
            b.Clips(b.Track(aura, AURA), 21.5, ("AC_AURA_Appear", 2.3), ("AC_AURA_Speak", end - 23.5));
            var auraShot = b.Cam("04_Aura", Control + V(-1.9f, 1.55f, -3.6f), b.Look("Look_Aura", Control + V(0.4f, 1.6f, -1.2f)), 42f, 0.2f);
            b.Shot(auraShot, 22.0, 9.0);

            // 5. Vista de la colonia al atardecer.
            b.Active(b.Ambience("Env_Dusk", "M_Sky_MarsDusk", true, DuskFog, 0.006f, DuskSky, DuskEquator, DuskGround), "Env_Dusk", (31.0, end - 31.0));
            b.SunMotion(0.0, end, new[]
            {
                (0f, 9f, 1f, new Color(1f, 0.8f, 0.62f)),
                (39f, 4f, 0.55f, new Color(0.75f, 0.72f, 0.9f)),
            });
            var colony = b.Dolly("05_Colony", new[] { V(-35f, 6f, 20f), V(-55f, 18f, 30f), V(-80f, 30f, 25f) }, b.Look("Look_Colony", V(2f, 3f, 0f)), 48f, 31.0, end - 31.0);
            b.Shot(colony, 31.0, end - 31.0);

            b.Line(AURA, "La tripulación está a salvo.", 1.5f, 4.5f);
            b.Line(AURA, "No hemos podido terminar todos los experimentos.", 9f, 13f);
            b.Line(AURA, "Pero aprendimos algo fundamental: en una misión espacial, proteger la vida siempre es una prioridad.", 22.5f, 30f);

            b.Finish("M_Sky_MarsDay", DayFog, 0.006f, DaySky, DayEquator, DayGround);
        }
#endif
    }
}

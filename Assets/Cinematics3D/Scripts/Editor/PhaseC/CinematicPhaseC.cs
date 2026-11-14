using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Fase C — Cinemáticas narrativas: intro, sol1_night, solar_alert y sol3_start.
    /// Cada una se genera en su propia escena (CIN_&lt;id&gt;) con su Timeline, cámaras Cinemachine,
    /// acciones, ambientes y subtítulos, y queda registrada en el catálogo y en la build.
    /// Volver a ejecutarla regenera estas cuatro escenas (no toca los recursos de la Fase B).
    /// </summary>
    internal static class CinematicPhaseC
    {
        const string Title = "SOL-3 · Cinemáticas · Fase C";

        // Estaciones dentro de cada escena (los interiores están lejos del terreno para no verse entre sí).
        static readonly Vector3 Space = new Vector3(-6000f, 0f, 0f);
        static readonly Vector3 Control = new Vector3(6000f, 0f, 0f);
        static readonly Vector3 Power = new Vector3(8000f, 0f, 0f);
        static readonly Vector3 Shelter = new Vector3(10000f, 0f, 0f);

        // Ambientes
        static readonly Color DayFog = new Color(0.74f, 0.50f, 0.34f);
        static readonly Color DaySky = new Color(0.62f, 0.47f, 0.39f), DayEquator = new Color(0.45f, 0.31f, 0.24f), DayGround = new Color(0.20f, 0.13f, 0.10f);
        static readonly Color DuskFog = new Color(0.36f, 0.34f, 0.42f);
        static readonly Color DuskSky = new Color(0.36f, 0.38f, 0.50f), DuskEquator = new Color(0.30f, 0.25f, 0.25f), DuskGround = new Color(0.12f, 0.09f, 0.08f);
        static readonly Color NightFog = new Color(0.08f, 0.08f, 0.12f);
        static readonly Color NightSky = new Color(0.10f, 0.11f, 0.17f), NightEquator = new Color(0.07f, 0.07f, 0.10f), NightGround = new Color(0.03f, 0.03f, 0.04f);
        static readonly Color SpaceSky = new Color(0.06f, 0.06f, 0.08f), SpaceEquator = new Color(0.04f, 0.04f, 0.05f), SpaceGround = new Color(0.02f, 0.02f, 0.02f);

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
                    "Se generarán (o regenerarán) las cuatro cinemáticas narrativas:\n\n" +
                    "• CIN_intro (≈76 s)\n• CIN_sol1_night (≈33 s)\n• CIN_solar_alert (≈41 s)\n• CIN_sol3_start (≈40 s)\n\n" +
                    "Cada una con su Timeline, cámaras, animaciones, ambientes y subtítulos, registrada en el catálogo y en la build. " +
                    "Al terminar se abrirá CIN_intro.", "Generar", "Cancelar"))
                return;

            var report = Run();
            string log = report.Publish("Fase C", "SOL3_Cinematics_PhaseC.txt");
            var validation = CinematicValidator.Validate();
            string validationLog = validation.Publish("Validación del sistema de cinemáticas", "SOL3_Cinematics_Validation.txt");
            EditorUtility.DisplayDialog(Title,
                $"Fase C: {report.Summary}\nValidación: {validation.Summary}\n\n" +
                "Abre cualquier escena CIN_<id> de Assets/Cinematics3D/Scenes y pulsa Play para verla con subtítulos.\n\n" +
                $"Informes en la Consola y en:\n{log}\n{validationLog}", "Cerrar");
        }

        public static CinematicReport Run()
        {
            var report = new CinematicReport();
#if !(SOL3_CINEMACHINE && SOL3_SPLINES)
            report.Error("Fase C", "Cinemachine o Splines no están instalados.");
            return report;
#else
            if (!CinematicPhaseB.IsPhaseAReady())
            {
                report.Error("Fase C", "La Fase A no está configurada. Ejecuta antes 'Fase B · Crear recursos y galería'.");
                return report;
            }
            foreach (var path in CinematicPhaseB.ExpectedPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    report.Error("Fase C", $"Falta {path}. Ejecuta antes 'Fase B · Crear recursos y galería'.");
                    return report;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.Warning("Fase C", "Cancelado: hay escenas con cambios sin guardar.");
                return report;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            int rendererIndex = catalog.Globals.cinematicRendererIndex;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CinematicEditorPaths.TemplateScene) == null)
                CinematicTemplateBuilder.Build(CinematicLayers.Index, rendererIndex, false, report);

            var ctx = new BuildContext { Overwrite = false, Layer = CinematicLayers.Index, Report = report };
            LoadExisting(ctx);

            Step(ctx, "intro", 0.1f, () => Intro(ctx, rendererIndex));
            Step(ctx, "sol1_night", 0.35f, () => Sol1Night(ctx, rendererIndex));
            Step(ctx, "solar_alert", 0.6f, () => SolarAlert(ctx, rendererIndex));
            Step(ctx, "sol3_start", 0.85f, () => Sol3Start(ctx, rendererIndex));
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CinematicEditorPaths.SceneFor(CinematicIds.Intro)) != null)
                EditorSceneManager.OpenScene(CinematicEditorPaths.SceneFor(CinematicIds.Intro), OpenSceneMode.Single);
            report.Info("Fase C", "Personajes y escenarios siguen siendo provisionales (Fase B); las cinemáticas los usan como prefabs, así que mejorarán solas al sustituirlos.");
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

        /// <summary>
        /// Registra los recursos de la Fase B. Se llama después de abrir cada escena nueva, porque
        /// abrir una escena descarga de memoria los assets que no estén referenciados.
        /// </summary>
        internal static void LoadExisting(BuildContext ctx)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { CinematicEditorPaths.Materials }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material != null)
                    ctx.Materials[material.name] = material;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { CinematicEditorPaths.Prefabs }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null)
                    ctx.Prefabs[prefab.name] = prefab;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { CinematicEditorPaths.Animations }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null)
                    ctx.Clips[clip.name] = clip;
            }
        }

#if SOL3_CINEMACHINE && SOL3_SPLINES
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static GameObject Child(GameObject parent, string path)
        {
            var child = parent != null ? parent.transform.Find(path) : null;
            return child != null ? child.gameObject : null;
        }

        static GameObject SpaceAmbience(CinematicSceneBuilder b) =>
            b.Ambience("Env_Space", "M_Sky_Space", false, Color.black, 0f, SpaceSky, SpaceEquator, SpaceGround);

        // ================================================================== INTRO

        static void Intro(BuildContext ctx, int renderer)
        {
            var b = new CinematicSceneBuilder(ctx, CinematicIds.Intro, renderer);
            var set = b.Place("PF_Set_MarsSurface", b.Environment, Vector3.zero, Vector3.zero);
            b.Place("PF_FX_MarsDust", b.Fx, V(0f, 3f, 0f), Vector3.zero);
            b.Place("PF_Space_Mars", b.Environment, Space, Vector3.zero);
            b.Place("PF_Space_Sun", b.Environment, Space + V(-450f, 150f, 600f), Vector3.zero);
            var control = b.Place("PF_Room_ControlCenter", b.Environment, Control, Vector3.zero);
            var astronaut = b.Place("PF_Astronaut", b.Actors, Control + V(1.7f, 0f, -1.2f), V(0f, -90f, 0f));
            var aura = b.Place("PF_AURA", b.Actors, Control + V(0f, 1.75f, -1.2f), Vector3.zero);

            // 1-2. Marte desde el espacio y acercamiento progresivo.
            b.Active(SpaceAmbience(b), "Env_Space", (0.0, 11.0));
            var mars = b.Dolly("01_MarsOrbit", new[] { Space + V(-60f, 40f, -330f), Space + V(-30f, 25f, -230f), Space + V(0f, 15f, -150f) },
                b.Look("Look_Mars", Space), 40f, 0.0, 11.0);
            b.Shot(mars, 0.0, 11.0);
            var descent = b.Dolly("02_Descent", new[] { V(-120f, 140f, -160f), V(-60f, 60f, -90f), V(-25f, 14f, -38f) },
                b.Look("Look_Hub", V(0f, 3f, 0f)), 50f, 11.0, 11.0);
            b.Shot(descent, 11.0, 11.0);

            // 3-4. Base Aurora desde el exterior y estructura de la colonia.
            var orbit = b.Dolly("03_BaseOrbit", new[] { V(-30f, 8f, -28f), V(10f, 12f, -38f), V(40f, 10f, -12f), V(38f, 9f, 20f) },
                b.Look("Look_Base", V(6f, 3f, 0f)), 45f, 22.0, 12.0);
            b.Shot(orbit, 22.0, 12.0);
            var solar = b.Track(Child(set, "BaseAurora/SolarArray"), "SolarArray", true);
            b.Clips(solar, 22.0, ("AC_SolarArray_Track", 54.3));

            // 5-6. Interior y presentación del astronauta.
            var interior = b.Dolly("04_Interior", new[] { Control + V(-5f, 2.4f, -4f), Control + V(-3f, 2.1f, -3.4f), Control + V(-1.2f, 1.9f, -3f) },
                b.Look("Look_Interior", Control + V(0.6f, 1.4f, 0.4f)), 55f, 34.0, 7.0);
            b.Shot(interior, 34.0, 7.0);
            var hero = b.Cam("05_Astronaut", Control + V(-0.6f, 1.7f, -2.6f), b.Look("Look_Astronaut", Control + V(1.7f, 1.35f, -1.2f)), 38f, 0.25f);
            b.Shot(hero, 41.0, 7.0);
            var astronautTrack = b.Track(astronaut, CinematicBindingKeys.Astronaut);
            b.Clips(astronautTrack, 33.0, ("AC_Astronaut_Idle", 4.3), ("AC_Astronaut_CheckWrist", 3.0), ("AC_Astronaut_LookAround", 5.0),
                ("AC_Astronaut_Idle", 8.0), ("AC_Astronaut_Gesture", 3.0), ("AC_Astronaut_Idle", 21.5));

            // 7. AURA aparece en la mesa holográfica.
            var reveal = b.Cam("06_AuraReveal", Control + V(-1.9f, 1.55f, -3.6f), b.Look("Look_Reveal", Control + V(0.4f, 1.6f, -1.2f)), 42f, 0.2f);
            b.Shot(reveal, 48.0, 12.0);
            var auraClose = b.Cam("07_AuraClose", Control + V(2.7f, 1.75f, -2.1f), b.Look("Look_Aura", Control + V(0f, 1.75f, -1.2f)), 34f, 0.2f);
            b.Shot(auraClose, 60.0, 8.0);
            b.Active(aura, AURA, (51.5, 24.8));
            b.Clips(b.Track(aura, AURA), 51.5, ("AC_AURA_Appear", 2.3), ("AC_AURA_Speak", 22.8));

            // 8. Cierre: "Por ahora." con un parpadeo de las luces.
            var closing = b.Dolly("08_Closing", new[] { Control + V(-1.6f, 1.8f, -3.3f), Control + V(-3.4f, 2.3f, -4.1f) },
                b.Look("Look_Closing", Control + V(0.4f, 1.5f, -1.2f)), 45f, 68.0, 8.3);
            b.Shot(closing, 68.0, 8.3);
            b.Active(Child(control, "Lights_Normal"), "Lights_Normal", (0.0, 70.8), (71.0, 5.3));

            b.Line(CT, "Durante generaciones, observamos Marte desde la distancia...", 0.8f, 5.8f);
            b.Line(CT, "...hasta que finalmente aprendimos a vivir aquí.", 6.2f, 9.8f);
            b.Line(CT, "Esta es Aurora. Una base experimental construida para descubrir cómo sobrevivir lejos de la Tierra.", 13f, 20.5f);
            b.Line(AURA, "Bienvenido a Base Aurora.", 53f, 55.8f);
            b.Line(AURA, "Soy AURA, tu asistente de misión.", 56.2f, 59.6f);
            b.Line(AURA, "Durante los próximos tres soles, nuestra supervivencia dependerá de tus decisiones.", 60f, 66f);
            b.Line(AURA, "Todos los sistemas están operativos.", 66.5f, 69.5f);
            b.Line(AURA, "Por ahora.", 70.6f, 72.6f);

            b.Finish("M_Sky_MarsDay", DayFog, 0.006f, DaySky, DayEquator, DayGround);
        }

        // ================================================================== PRIMERA NOCHE

        static void Sol1Night(BuildContext ctx, int renderer)
        {
            const double end = 33.0;
            var b = new CinematicSceneBuilder(ctx, CinematicIds.Sol1Night, renderer);
            var set = b.Place("PF_Set_MarsSurface", b.Environment, Vector3.zero, Vector3.zero);
            b.Place("PF_FX_MarsDust", b.Fx, V(0f, 3f, 0f), Vector3.zero);
            var power = b.Place("PF_Room_PowerRoom", b.Environment, Power, Vector3.zero);
            var aura = b.Place("PF_AURA", b.Actors, Power + V(-1.6f, 1.7f, 0f), V(0f, 30f, 0f));

            // 1. La luz disminuye sobre Marte.
            b.SunMotion(0.0, end, new[]
            {
                (0f, 10f, 1.2f, new Color(1f, 0.78f, 0.6f)),
                (14f, 1.5f, 0.15f, new Color(0.55f, 0.6f, 0.85f)),
            });
            var wide = b.Dolly("01_Dusk", new[] { V(-45f, 12f, -55f), V(-20f, 9f, -60f), V(5f, 7f, -56f) },
                b.Look("Look_Dusk", V(8f, 2f, -12f)), 45f, 0.0, 8.0);
            b.Shot(wide, 0.0, 8.0);

            // 2. Los paneles solares reducen su producción y se pliegan.
            var panels = b.Cam("02_SolarPanels", V(2f, 2.2f, -33f), b.Look("Look_Panels", V(12f, 1.4f, -25f)), 40f, 0.2f);
            b.Shot(panels, 8.0, 6.0);
            b.Clips(b.Track(Child(set, "BaseAurora/SolarArray"), "SolarArray", true), 8.5, ("AC_SolarArray_Stow", end - 8.5));
            b.Active(Child(set, "BaseAurora/SolarArray/OutputLight"), "SolarOutputLight", (0.0, 11.0));

            // 3-4. Sala de máquinas: las baterías entran en funcionamiento.
            var room = b.Dolly("03_PowerRoom", new[] { Power + V(3.5f, 2.2f, -3.4f), Power + V(2f, 1.9f, -2.8f), Power + V(0.6f, 1.8f, -2.3f) },
                b.Look("Look_Batteries", Power + V(-1.5f, 1.4f, 3.2f)), 50f, 14.0, 8.0);
            b.Shot(room, 14.0, 8.0);
            b.Clips(b.Track(Child(power, "BatteryBank"), "BatteryBank", true), 14.5, ("AC_Battery_Drain", end - 14.5));

            // 5. AURA notifica la situación.
            b.Active(aura, AURA, (15.0, end - 15.0));
            b.Clips(b.Track(aura, AURA), 15.0, ("AC_AURA_Appear", 2.3), ("AC_AURA_Speak", end - 17.0));
            var auraClose = b.Cam("04_Aura", Power + V(0.5f, 1.8f, -1.7f), b.Look("Look_Aura", Power + V(-1.6f, 1.7f, 0f)), 34f, 0.2f);
            b.Shot(auraClose, 22.0, 5.5);

            // 6. Transición al ciclo nocturno.
            b.Active(b.Ambience("Env_Night", "M_Sky_MarsNight", true, NightFog, 0.008f, NightSky, NightEquator, NightGround), "Env_Night", (27.5, end - 27.5));
            var night = b.Cam("05_Night", V(-40f, 10f, -45f), b.Look("Look_Night", V(0f, 3f, 0f)), 50f, 0.15f);
            b.Shot(night, 27.5, end - 27.5);

            b.Line(AURA, "Atención. La producción solar está disminuyendo.", 1.5f, 5.5f);
            b.Line(AURA, "Durante la noche dependeremos de la energía almacenada.", 15f, 20f);
            b.Line(AURA, "Espero que hayas administrado bien nuestras reservas.", 22.5f, 27.5f);

            b.Finish("M_Sky_MarsDusk", DuskFog, 0.006f, DuskSky, DuskEquator, DuskGround);
        }

        // ================================================================== ALERTA SOLAR

        static void SolarAlert(BuildContext ctx, int renderer)
        {
            const double end = 41.0;
            var b = new CinematicSceneBuilder(ctx, CinematicIds.SolarAlert, renderer);
            b.Place("PF_Space_Sun", b.Environment, Space, Vector3.zero);
            b.Place("PF_Space_Mars", b.Environment, Space + V(350f, -60f, 700f), Vector3.zero);
            var burst = b.Place("PF_FX_SolarFlare", b.Fx, Space, Vector3.zero, "Flare_Burst");
            var control = b.Place("PF_Room_ControlCenter", b.Environment, Control, Vector3.zero);
            b.Place("PF_Room_Shelter", b.Environment, Shelter, Vector3.zero);
            var astronaut = b.Place("PF_Astronaut", b.Actors, Control + V(0f, 0f, 1.9f), Vector3.zero);
            var aura = b.Place("PF_AURA", b.Actors, Control + V(0f, 1.75f, -1.2f), Vector3.zero);
            var sheltered = b.Place("PF_Astronaut", b.Actors, Shelter + V(0.3f, 0f, 0.2f), V(0f, 160f, 0f), "PF_Astronaut_Shelter");
            if (sheltered != null)
                b.Key(sheltered, "Astronaut_Shelter");

            // 1-2. El Sol y la actividad solar (sin datos científicos inventados).
            b.Active(SpaceAmbience(b), "Env_Space", (0.0, 11.0));
            var sun = b.Dolly("01_Sun", new[] { Space + V(-60f, 20f, -260f), Space + V(-35f, 12f, -215f) }, b.Look("Look_Sun", Space), 45f, 0.0, 6.5);
            b.Shot(sun, 0.0, 6.5);
            var flare = b.Cam("02_SolarActivity", Space + V(90f, 40f, -140f), b.Look("Look_Flare", Space + V(0f, 10f, 0f)), 40f, 0f);
            b.Shot(flare, 6.5, 4.5);
            b.Active(burst, "Flare_Burst", (6.0, 5.0));

            // 3. La alerta llega al centro de control.
            var alertScreen = CinematicBuildKit.Prim(PrimitiveType.Quad, "AlertScreen", b.Environment, ctx.Mat("M_Screen_Alert"), ctx.Layer,
                Control + V(-5.98f, 2.3f, 0f), V(0f, -90f, 0f), V(3f, 1.5f, 1f), false);
            b.Active(alertScreen, "AlertScreen", (11.0, end - 11.0));
            b.Active(Child(control, "EmergencyLights"), "EmergencyLights", (11.0, end - 11.0));
            b.Active(Child(control, "Lights_Normal"), "Lights_Normal", (0.0, 11.5));
            var alert = b.Dolly("03_ControlAlert", new[] { Control + V(-4.5f, 2.2f, -3.8f), Control + V(-3f, 1.9f, -2.2f) },
                b.Look("Look_Console", Control + V(0f, 1.3f, 2f)), 50f, 11.0, 8.0);
            b.Shot(alert, 11.0, 8.0);
            b.Clips(b.Track(astronaut, CinematicBindingKeys.Astronaut), 11.0, ("AC_Astronaut_WorkConsole", 4.0), ("AC_Astronaut_CheckWrist", 3.0),
                ("AC_Astronaut_Concern", 3.0), ("AC_Astronaut_Idle", end - 20.1));

            // 4. AURA analiza la situación.
            b.Active(aura, AURA, (18.5, end - 18.5));
            b.Clips(b.Track(aura, AURA), 18.5, ("AC_AURA_Appear", 2.3), ("AC_AURA_Alert", end - 20.5));
            var analysis = b.Cam("04_AuraAnalysis", Control + V(-1.8f, 1.6f, -3.4f), b.Look("Look_Aura", Control + V(0f, 1.75f, -1.2f)), 40f, 0.25f);
            b.Shot(analysis, 19.0, 8.0);

            // 5. El refugio y los sistemas de protección.
            var shelter = b.Cam("05_Shelter", Shelter + V(-2.6f, 1.7f, -2.4f), b.Look("Look_Shelter", Shelter + V(0.4f, 1.1f, 1f)), 55f, 0.3f);
            b.Shot(shelter, 27.0, 7.0);
            b.Clips(b.Track(sheltered, "Astronaut_Shelter"), 27.0, ("AC_Astronaut_Idle", 1.5), ("AC_Astronaut_CheckWrist", 3.0),
                ("AC_Astronaut_LookAround", 5.0), ("AC_Astronaut_Idle", 6.0));

            // 6. Las decisiones que tomará el jugador.
            var decision = b.Dolly("06_Decision", new[] { Control + V(-3f, 1.9f, -3.6f), Control + V(-1.8f, 1.7f, -2.6f) },
                b.Look("Look_Decision", Control + V(0.3f, 1.5f, 0.2f)), 45f, 34.0, end - 34.0);
            b.Shot(decision, 34.0, end - 34.0);

            b.Line(AURA, "Atención. Hemos recibido información sobre actividad solar.", 1f, 5.5f);
            b.Line(AURA, "Debemos revisar las condiciones de seguridad de la tripulación.", 12f, 17f);
            b.Line(AURA, "Podemos interrumpir las actividades exteriores, preparar el refugio y proteger los recursos esenciales.", 19.5f, 26.5f);
            b.Line(AURA, "Pero tendremos que decidir qué operaciones detener.", 34.5f, 39.5f);

            b.Finish("M_Sky_MarsDay", DayFog, 0.006f, DaySky, DayEquator, DayGround);
        }

        // ================================================================== TERCER SOL

        static void Sol3Start(BuildContext ctx, int renderer)
        {
            const double end = 40.0;
            var b = new CinematicSceneBuilder(ctx, CinematicIds.Sol3Start, renderer);
            var set = b.Place("PF_Set_MarsSurface", b.Environment, Vector3.zero, Vector3.zero);
            b.Place("PF_FX_MarsDust", b.Fx, V(0f, 3f, 0f), Vector3.zero);
            var power = b.Place("PF_Room_PowerRoom", b.Environment, Power, Vector3.zero);
            b.Place("PF_Room_ControlCenter", b.Environment, Control, Vector3.zero);
            var astronaut = b.Place("PF_Astronaut", b.Actors, Control + V(0f, 0f, 1.9f), Vector3.zero);
            var aura = b.Place("PF_AURA", b.Actors, Control + V(0f, 1.75f, -1.2f), Vector3.zero);

            // 1. Amanecer marciano.
            b.SunMotion(0.0, end, new[]
            {
                (0f, 0.5f, 0.25f, new Color(0.7f, 0.75f, 0.95f)),
                (9f, 16f, 1.5f, new Color(1f, 0.85f, 0.7f)),
            });
            var dawn = b.Dolly("01_Dawn", new[] { V(60f, 6f, -60f), V(46f, 7f, -70f) }, b.Look("Look_Dawn", V(0f, 4f, 0f)), 50f, 0.0, 9.0);
            b.Shot(dawn, 0.0, 9.0);
            b.Active(b.Ambience("Env_Day", "M_Sky_MarsDay", true, DayFog, 0.006f, DaySky, DayEquator, DayGround), "Env_Day", (9.0, end - 9.0));

            // 2. Recorrido por la base.
            var tour = b.Dolly("02_BaseTour", new[] { V(-22f, 5f, 10f), V(-21f, 5f, -11f), V(-5f, 6f, -24f) }, b.Look("Look_Tour", V(-6f, 3f, 0f)), 50f, 9.0, 8.0);
            b.Shot(tour, 9.0, 8.0);

            // 3. Los sistemas siguen funcionando.
            var panels = b.Cam("03_SolarTracking", V(2f, 3f, -34f), b.Look("Look_Panels", V(14f, 1.5f, -25f)), 42f, 0.2f);
            b.Shot(panels, 17.0, 4.0);
            b.Clips(b.Track(Child(set, "BaseAurora/SolarArray"), "SolarArray", true), 16.5, ("AC_SolarArray_Track", end - 16.5));
            var batteries = b.Cam("04_Batteries", Power + V(3f, 2f, -3f), b.Look("Look_Batteries", Power + V(-1.5f, 1.3f, 3.2f)), 50f, 0.25f);
            b.Shot(batteries, 21.0, 4.0);
            b.Clips(b.Track(Child(power, "BatteryBank"), "BatteryBank", true), 20.5, ("AC_Battery_Charge", end - 20.5));

            // 4. El astronauta se prepara para la última jornada.
            var prep = b.Cam("05_Preparation", Control + V(-1.6f, 1.6f, 1.2f), b.Look("Look_Prep", Control + V(0f, 1.4f, 2f)), 45f, 0.25f);
            b.Shot(prep, 25.0, 6.0);
            b.Clips(b.Track(astronaut, CinematicBindingKeys.Astronaut), 24.5, ("AC_Astronaut_CheckWrist", 3.0), ("AC_Astronaut_WorkConsole", 4.0),
                ("AC_Astronaut_Idle", 2.0), ("AC_Astronaut_Gesture", 3.0), ("AC_Astronaut_Idle", 10.0));

            // 5. AURA presenta los objetivos finales.
            b.Active(aura, AURA, (30.5, end - 30.5));
            b.Clips(b.Track(aura, AURA), 30.5, ("AC_AURA_Appear", 2.3), ("AC_AURA_Speak", end - 32.5));
            var objectives = b.Cam("06_Objectives", Control + V(-1.8f, 1.6f, -3.4f), b.Look("Look_Objectives", Control + V(0.2f, 1.6f, -0.6f)), 42f, 0.2f);
            b.Shot(objectives, 31.0, end - 31.0);

            b.Line(AURA, "Hemos llegado al último sol de nuestra prueba.", 1.5f, 5.5f);
            b.Line(AURA, "Pero la misión todavía no ha terminado.", 9.5f, 13f);
            b.Line(AURA, "Debemos mantener los sistemas esenciales, completar nuestra investigación y transmitir los resultados.", 24.5f, 32f);
            b.Line(AURA, "El futuro de Aurora depende de lo que hagamos hoy.", 33f, 38f);

            b.Finish("M_Sky_MarsDusk", DuskFog, 0.006f, DuskSky, DuskEquator, DuskGround);
        }
#endif
    }
}

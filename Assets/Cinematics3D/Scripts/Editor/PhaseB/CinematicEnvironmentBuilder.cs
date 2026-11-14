using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Escenarios, props y efectos provisionales (primitivas + mallas procedurales).
    /// Cada escenario es un prefab independiente que se coloca en las escenas cinemáticas;
    /// los props se anidan como prefabs, así que sustituir un prop actualiza todos los escenarios.
    /// Las salas incluyen su propio grupo de luces (Lights) para ser autosuficientes.
    /// </summary>
    internal static class CinematicEnvironmentBuilder
    {
        const int PanelsPerRow = 6;
        const int BatteryRacks = 8;

        public static void BuildAll(BuildContext ctx)
        {
            BuildSharedMeshes(ctx);
            BuildPropClips(ctx);
            BuildFx(ctx);
            BuildProps(ctx);

            var env = PhaseBPaths.Environment;
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Mars_Terrain", () => MarsTerrain(ctx));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Mars_RockField", () => RockField(ctx));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_BaseAurora_Exterior", () => BaseExterior(ctx));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Set_MarsSurface", () => MarsSurfaceSet(ctx));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Room_ControlCenter", () => ControlCenter(ctx));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Room_PowerRoom", () => PowerRoom(ctx));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Room_Shelter", () => Shelter(ctx));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Room_EarthControl", () => EarthControl(ctx));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Space_Mars", () => Planet(ctx, "M_Mars_Planet", "M_Atmo_Mars", 100f, 25f));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Space_Earth", () => Planet(ctx, "M_Earth_Planet", "M_Atmo_Earth", 12f, 23f));
            CinematicBuildKit.GetPrefab(ctx, env, "PF_Space_Sun", () => Sun(ctx));
        }

        // ================================================================== Mallas compartidas

        static void BuildSharedMeshes(BuildContext ctx)
        {
            CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Hemisphere(32, 12), "MSH_Hemisphere");
            CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Torus(0.5f, 0.012f, 64, 6), "MSH_Ring_Unit");
            CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Cone(0.12f, 0.7f, 1.3f, 32), "MSH_HoloBeam");
            CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Terrain(400f, 120, 3, 45f, 14f), "MSH_Mars_Terrain");
            for (int i = 0; i < 3; i++)
                CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Icosphere(2, 0.45f, 11 + i * 7, true, new Vector3(1f, 0.65f + i * 0.1f, 1f)), $"MSH_Rock_{i + 1:00}");
        }

        // ================================================================== Animaciones de props

        static void BuildPropClips(BuildContext ctx)
        {
            string folder = PhaseBPaths.AnimProps;

            var track = new PoseClipBuilder("AC_SolarArray_Track", false, new Dictionary<string, Vector3>());
            var stow = new PoseClipBuilder("AC_SolarArray_Stow", false, new Dictionary<string, Vector3>());
            foreach (var row in new[] { "Row_A", "Row_B" })
            {
                for (int i = 1; i <= PanelsPerRow; i++)
                {
                    string path = $"SolarArray_Rows/{row}/Panel_{i:00}/Tilt";
                    float delay = (i - 1) * 0.12f;
                    track.Curve(path, typeof(Transform), "localEulerAnglesRaw.x", PoseClipBuilder.Keys(new[] { 0f, 1f + delay, 8f }, new[] { -55f, -55f, -20f }));
                    track.Curve(path, typeof(Transform), "localEulerAnglesRaw.y", PoseClipBuilder.Constant(8f, 0f));
                    track.Curve(path, typeof(Transform), "localEulerAnglesRaw.z", PoseClipBuilder.Constant(8f, 0f));
                    stow.Curve(path, typeof(Transform), "localEulerAnglesRaw.x", PoseClipBuilder.Keys(new[] { 0f, 0.5f + delay, 4f }, new[] { -35f, -35f, 0f }));
                    stow.Curve(path, typeof(Transform), "localEulerAnglesRaw.y", PoseClipBuilder.Constant(4f, 0f));
                    stow.Curve(path, typeof(Transform), "localEulerAnglesRaw.z", PoseClipBuilder.Constant(4f, 0f));
                }
            }
            CinematicBuildKit.SaveClip(ctx, track.Build(), folder);
            CinematicBuildKit.SaveClip(ctx, stow.Build(), folder);

            var charge = new PoseClipBuilder("AC_Battery_Charge", false, new Dictionary<string, Vector3>());
            var drain = new PoseClipBuilder("AC_Battery_Drain", false, new Dictionary<string, Vector3>());
            for (int i = 1; i <= BatteryRacks; i++)
            {
                string path = $"Racks/Rack_{i:00}/Level";
                float jitter = (i % 3) * 0.06f;
                charge.Curve(path, typeof(Transform), "m_LocalScale.x", PoseClipBuilder.Constant(4f, 1f));
                charge.Curve(path, typeof(Transform), "m_LocalScale.y", PoseClipBuilder.Keys(new[] { 0f, 4f }, new[] { 0.15f + jitter, 1f }));
                charge.Curve(path, typeof(Transform), "m_LocalScale.z", PoseClipBuilder.Constant(4f, 1f));
                drain.Curve(path, typeof(Transform), "m_LocalScale.x", PoseClipBuilder.Constant(6f, 1f));
                drain.Curve(path, typeof(Transform), "m_LocalScale.y", PoseClipBuilder.Keys(new[] { 0f, 6f }, new[] { 0.9f, 0.25f + jitter }));
                drain.Curve(path, typeof(Transform), "m_LocalScale.z", PoseClipBuilder.Constant(6f, 1f));
            }
            CinematicBuildKit.SaveClip(ctx, charge.Build(), folder);
            CinematicBuildKit.SaveClip(ctx, drain.Build(), folder);

            CinematicBuildKit.SaveClip(ctx, Spin("AC_Beacon_Spin", "Rotor", 1f), folder);
            CinematicBuildKit.SaveClip(ctx, Spin("AC_Planet_Rotate", "Planet", 120f), folder);

            var scan = new PoseClipBuilder("AC_Dish_Scan", true, new Dictionary<string, Vector3>());
            scan.Key(0f, new Dictionary<string, Vector3> { { "DishPivot", new Vector3(0f, -25f, 0f) } })
                .Key(4f, new Dictionary<string, Vector3> { { "DishPivot", new Vector3(0f, 25f, 0f) } })
                .Key(8f, new Dictionary<string, Vector3> { { "DishPivot", new Vector3(0f, -25f, 0f) } });
            CinematicBuildKit.SaveClip(ctx, scan.Build(), folder);
        }

        static AnimationClip Spin(string name, string path, float seconds)
        {
            var builder = new PoseClipBuilder(name, true, new Dictionary<string, Vector3>());
            builder.Curve(path, typeof(Transform), "localEulerAnglesRaw.x", PoseClipBuilder.Constant(seconds, 0f));
            builder.Curve(path, typeof(Transform), "localEulerAnglesRaw.y", PoseClipBuilder.Linear(seconds, 0f, 360f));
            builder.Curve(path, typeof(Transform), "localEulerAnglesRaw.z", PoseClipBuilder.Constant(seconds, 0f));
            return builder.Build();
        }

        // ================================================================== Efectos

        static void BuildFx(BuildContext ctx)
        {
            string fx = PhaseBPaths.Fx;
            CinematicBuildKit.GetPrefab(ctx, fx, "PF_FX_MarsDust", () =>
            {
                var go = CinematicBuildKit.Obj("Dust", null, ctx.Layer);
                var ps = Particles(go, ctx.Mat("M_FX_Dust"), 150, ParticleSystemRenderMode.Billboard);
                var main = ps.main;
                main.duration = 10f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(4f, 9f);
                main.startColor = new Color(0.85f, 0.58f, 0.38f, 1f);
                main.prewarm = true;
                var emission = ps.emission;
                emission.rateOverTime = 12f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(80f, 6f, 80f);
                Velocity(ps, new Vector2(1.5f, 3f), new Vector2(-0.1f, 0.2f), new Vector2(-0.5f, 0.5f));
                FadeInOut(ps, new Color(0.85f, 0.58f, 0.38f), 0.18f);
                return go;
            });

            CinematicBuildKit.GetPrefab(ctx, fx, "PF_FX_SolarFlare", () =>
            {
                var go = CinematicBuildKit.Obj("SolarFlare", null, ctx.Layer);
                var ps = Particles(go, ctx.Mat("M_FX_Glow"), 220, ParticleSystemRenderMode.Billboard);
                var main = ps.main;
                main.duration = 4f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 14f);
                main.startSize = new ParticleSystem.MinMaxCurve(4f, 11f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(3f, 1.4f, 0.4f, 1f), new Color(3f, 2.2f, 0.9f, 1f));
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var emission = ps.emission;
                emission.rateOverTime = 28f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 20f;
                FadeInOut(ps, new Color(1f, 0.7f, 0.3f), 1f);
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                return go;
            });

            CinematicBuildKit.GetPrefab(ctx, fx, "PF_FX_Sparks", () =>
            {
                var go = CinematicBuildKit.Obj("Sparks", null, ctx.Layer);
                var ps = Particles(go, ctx.Mat("M_FX_Glow"), 120, ParticleSystemRenderMode.Stretch);
                var main = ps.main;
                main.duration = 1.6f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(3f, 2f, 0.6f, 1f), new Color(3f, 1.2f, 0.3f, 1f));
                main.gravityModifier = 0.38f; // gravedad marciana ≈ 0,38 g
                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)35), new ParticleSystem.Burst(0.5f, (short)12) });
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 35f;
                shape.radius = 0.05f;
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 1f;
                return go;
            });

            CinematicBuildKit.GetPrefab(ctx, fx, "PF_FX_TransmissionPulse", () =>
            {
                var go = CinematicBuildKit.Obj("TransmissionPulse", null, ctx.Layer, Vector3.zero, new Vector3(-90f, 0f, 0f));
                var ps = Particles(go, ctx.Mat("M_FX_Glow"), 40, ParticleSystemRenderMode.Billboard);
                var main = ps.main;
                main.duration = 2f;
                main.startLifetime = 3f;
                main.startSpeed = 25f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                main.startColor = new Color(0.9f, 2.6f, 3f, 1f);
                var emission = ps.emission;
                emission.rateOverTime = 5f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 3f;
                shape.radius = 0.2f;
                FadeInOut(ps, new Color(0.33f, 0.78f, 0.85f), 1f);
                return go;
            });
        }

        static ParticleSystem Particles(GameObject go, Material material, int maxParticles, ParticleSystemRenderMode mode)
        {
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.useUnscaledTime = true;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static void Velocity(ParticleSystem ps, Vector2 x, Vector2 y, Vector2 z)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(x.x, x.y);
            velocity.y = new ParticleSystem.MinMaxCurve(y.x, y.y);
            velocity.z = new ParticleSystem.MinMaxCurve(z.x, z.y);
        }

        static void FadeInOut(ParticleSystem ps, Color color, float peakAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.25f), new GradientAlphaKey(peakAlpha, 0.7f), new GradientAlphaKey(0f, 1f) });
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        // ================================================================== Props

        static void BuildProps(BuildContext ctx)
        {
            string props = PhaseBPaths.Props;
            int layer = ctx.Layer;

            CinematicBuildKit.GetPrefab(ctx, props, "PF_Prop_Console", () =>
            {
                var root = CinematicBuildKit.Obj("Console", null, layer);
                Box(ctx, root, "Desk", "M_Base_Graphite", new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(2.4f, 0.9f, 0.8f));
                Box(ctx, root, "DeskTop", "M_Base_Steel", new Vector3(0f, 0.92f, -0.05f), Vector3.zero, new Vector3(2.5f, 0.05f, 0.95f));
                Box(ctx, root, "ScreenFrame", "M_Base_Graphite", new Vector3(0f, 1.35f, 0.25f), new Vector3(-15f, 0f, 0f), new Vector3(2.2f, 0.85f, 0.06f));
                var screen = CinematicBuildKit.Prim(PrimitiveType.Quad, "Screen", root.transform, ctx.Mat("M_Screen_Data"), layer,
                    new Vector3(0f, 1.35f, 0.215f), new Vector3(-15f, 0f, 0f), new Vector3(2.08f, 0.76f, 1f), false);
                screen.GetComponent<MeshRenderer>().receiveShadows = false;
                for (int i = 0; i < 6; i++)
                {
                    string mat = i % 3 == 0 ? "M_Emit_Green" : i % 3 == 1 ? "M_Emit_Cyan" : "M_Emit_Orange";
                    Box(ctx, root, $"Button_{i + 1}", mat, new Vector3(-0.75f + i * 0.3f, 0.955f, -0.3f), Vector3.zero, new Vector3(0.12f, 0.02f, 0.08f), false);
                }
                return root;
            });

            CinematicBuildKit.GetPrefab(ctx, props, "PF_Prop_HoloTable", () =>
            {
                var root = CinematicBuildKit.Obj("HoloTable", null, layer);
                CinematicBuildKit.Prim(PrimitiveType.Cylinder, "Base", root.transform, ctx.Mat("M_Base_Graphite"), layer, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(1.6f, 0.45f, 1.6f));
                CinematicBuildKit.Prim(PrimitiveType.Cylinder, "Top", root.transform, ctx.Mat("M_Base_Steel"), layer, new Vector3(0f, 0.92f, 0f), Vector3.zero, new Vector3(1.7f, 0.03f, 1.7f));
                CinematicBuildKit.MeshObj("GlowRing", root.transform, ctx.MeshAsset("MSH_Ring_Unit"), ctx.Mat("M_Emit_Cyan"), layer, new Vector3(0f, 0.955f, 0f), Vector3.zero, new Vector3(1.5f, 1.5f, 1.5f), false);
                CinematicBuildKit.Prim(PrimitiveType.Cylinder, "Emitter", root.transform, ctx.Mat("M_Emit_Cyan"), layer, new Vector3(0f, 0.95f, 0f), Vector3.zero, new Vector3(0.25f, 0.02f, 0.25f), false);
                var beam = CinematicBuildKit.MeshObj("Beam", root.transform, ctx.MeshAsset("MSH_HoloBeam"), ctx.Mat("M_Holo_Beam"), layer, new Vector3(0f, 0.96f, 0f), Vector3.zero, Vector3.one, false);
                beam.GetComponent<MeshRenderer>().receiveShadows = false;
                CinematicBuildKit.Obj("Anchor_AURA", root.transform, layer, new Vector3(0f, 1.75f, 0f));
                return root;
            });

            CinematicBuildKit.GetPrefab(ctx, props, "PF_Prop_BatteryRack", () =>
            {
                var root = CinematicBuildKit.Obj("BatteryRack", null, layer);
                Box(ctx, root, "Cabinet", "M_Base_Graphite", new Vector3(0f, 1.1f, 0f), Vector3.zero, new Vector3(0.9f, 2.2f, 0.6f));
                Box(ctx, root, "Window", "M_Base_SpaceBlue", new Vector3(0f, 1.05f, -0.301f), Vector3.zero, new Vector3(0.62f, 1.9f, 0.01f), false);
                var level = CinematicBuildKit.Obj("Level", root.transform, layer, new Vector3(0f, 0.15f, -0.31f));
                level.transform.localScale = new Vector3(1f, 0.85f, 1f);
                Box(ctx, level, "Fill", "M_Emit_Green", new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(0.5f, 1.8f, 0.01f), false);
                Box(ctx, root, "StatusLight", "M_Emit_Cyan", new Vector3(0f, 2.12f, -0.31f), Vector3.zero, new Vector3(0.2f, 0.05f, 0.01f), false);
                return root;
            });

            var beaconController = CinematicBuildKit.GetLoopController(ctx, "AC_Beacon_Default", ctx.Clip("AC_Beacon_Spin"));
            CinematicBuildKit.GetPrefab(ctx, props, "PF_Prop_AlertBeacon", () =>
            {
                var root = CinematicBuildKit.Obj("AlertBeacon", null, layer);
                CinematicBuildKit.Prim(PrimitiveType.Cylinder, "Base", root.transform, ctx.Mat("M_Base_Graphite"), layer, new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(0.22f, 0.04f, 0.22f));
                CinematicBuildKit.MeshObj("Dome", root.transform, ctx.MeshAsset("MSH_Hemisphere"), ctx.Mat("M_Emit_Red"), layer, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.2f, 0.24f, 0.2f), false);
                var rotor = CinematicBuildKit.Obj("Rotor", root.transform, layer, new Vector3(0f, 0.12f, 0f));
                Box(ctx, rotor, "Lamp", "M_Emit_White", new Vector3(0f, 0f, 0.06f), Vector3.zero, new Vector3(0.06f, 0.05f, 0.04f), false);
                CinematicBuildKit.SetupAnimator(root, beaconController);
                return root;
            });

            CinematicBuildKit.GetPrefab(ctx, props, "PF_Prop_Crate", () =>
            {
                var root = CinematicBuildKit.Obj("Crate", null, layer);
                Box(ctx, root, "Body", "M_Base_Panel", new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(0.9f, 0.7f, 0.6f));
                Box(ctx, root, "Band", "M_Suit_Orange", new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(0.92f, 0.08f, 0.62f));
                return root;
            });

            CinematicBuildKit.GetPrefab(ctx, props, "PF_Prop_SolarPanel", () =>
            {
                var root = CinematicBuildKit.Obj("SolarPanel", null, layer);
                CinematicBuildKit.Prim(PrimitiveType.Cylinder, "Post", root.transform, ctx.Mat("M_Base_Steel"), layer, new Vector3(0f, 0.7f, 0f), Vector3.zero, new Vector3(0.12f, 0.7f, 0.12f));
                var tilt = CinematicBuildKit.Obj("Tilt", root.transform, layer, new Vector3(0f, 1.45f, 0f), new Vector3(-35f, 0f, 0f));
                Box(ctx, tilt, "Frame", "M_Base_Steel", Vector3.zero, Vector3.zero, new Vector3(3.1f, 0.06f, 1.75f));
                Box(ctx, tilt, "Cells", "M_SolarPanel", new Vector3(0f, 0.035f, 0f), Vector3.zero, new Vector3(3f, 0.02f, 1.65f));
                return root;
            });
        }

        static GameObject Box(BuildContext ctx, GameObject parent, string name, string material, Vector3 position, Vector3 euler, Vector3 scale, bool shadows = true) =>
            CinematicBuildKit.Prim(PrimitiveType.Cube, name, parent.transform, ctx.Mat(material), ctx.Layer, position, euler, scale, shadows);

        static GameObject Cyl(BuildContext ctx, GameObject parent, string name, string material, Vector3 position, Vector3 euler, Vector3 scale, bool shadows = true) =>
            CinematicBuildKit.Prim(PrimitiveType.Cylinder, name, parent.transform, ctx.Mat(material), ctx.Layer, position, euler, scale, shadows);

        static GameObject Dome(BuildContext ctx, GameObject parent, string name, string material, Vector3 position, Vector3 euler, Vector3 scale, bool shadows = true) =>
            CinematicBuildKit.MeshObj(name, parent.transform, ctx.MeshAsset("MSH_Hemisphere"), ctx.Mat(material), ctx.Layer, position, euler, scale, shadows);

        static GameObject Ring(BuildContext ctx, GameObject parent, string name, string material, Vector3 position, Vector3 euler, float diameter) =>
            CinematicBuildKit.MeshObj(name, parent.transform, ctx.MeshAsset("MSH_Ring_Unit"), ctx.Mat(material), ctx.Layer, position, euler, Vector3.one * diameter, false);

        // ================================================================== Marte

        static GameObject MarsTerrain(BuildContext ctx)
        {
            var root = CinematicBuildKit.Obj("MarsTerrain", null, ctx.Layer);
            CinematicBuildKit.MeshObj("Ground", root.transform, ctx.MeshAsset("MSH_Mars_Terrain"), ctx.Mat("M_Mars_Ground"), ctx.Layer, Vector3.zero, Vector3.zero, Vector3.one);
            return root;
        }

        static GameObject RockField(BuildContext ctx)
        {
            var root = CinematicBuildKit.Obj("RockField", null, ctx.Layer);
            var probe = new GameObject("TerrainProbe");
            var collider = probe.AddComponent<MeshCollider>();
            collider.sharedMesh = ctx.MeshAsset("MSH_Mars_Terrain");
            Physics.SyncTransforms();

            var random = new System.Random(21);
            var rockMaterial = ctx.Mat("M_Mars_Rock");
            for (int i = 0; i < 48; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = 38f + (float)random.NextDouble() * 150f;
                float scale = 0.4f + Mathf.Pow((float)random.NextDouble(), 2f) * 3.2f;
                var position = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                if (collider.Raycast(new Ray(position + Vector3.up * 200f, Vector3.down), out var hit, 400f))
                    position.y = hit.point.y - scale * 0.15f;
                var mesh = ctx.MeshAsset($"MSH_Rock_{i % 3 + 1:00}");
                CinematicBuildKit.MeshObj($"Rock_{i + 1:00}", root.transform, mesh, rockMaterial, ctx.Layer, position,
                    new Vector3(0f, (float)random.NextDouble() * 360f, 0f), Vector3.one * scale);
            }

            Object.DestroyImmediate(probe);
            return root;
        }

        static GameObject MarsSurfaceSet(BuildContext ctx)
        {
            var root = CinematicBuildKit.Obj("MarsSurface", null, ctx.Layer);
            CinematicBuildKit.Nest(ctx, "PF_Mars_Terrain", root.transform, Vector3.zero, Vector3.zero, "Terrain");
            CinematicBuildKit.Nest(ctx, "PF_Mars_RockField", root.transform, Vector3.zero, Vector3.zero, "RockField");
            CinematicBuildKit.Nest(ctx, "PF_BaseAurora_Exterior", root.transform, Vector3.zero, Vector3.zero, "BaseAurora");
            return root;
        }

        // ================================================================== Base Aurora (exterior)

        static GameObject BaseExterior(BuildContext ctx)
        {
            int layer = ctx.Layer;
            var root = CinematicBuildKit.Obj("BaseAurora", null, layer);

            // Módulo central
            var hub = CinematicBuildKit.Obj("Hub", root.transform, layer);
            Cyl(ctx, hub, "Foundation", "M_Base_Steel", new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(10.4f, 0.3f, 10.4f));
            Dome(ctx, hub, "Dome", "M_Base_Panel", new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(10f, 10f, 10f));
            Ring(ctx, hub, "WindowBand", "M_Emit_Cyan", new Vector3(0f, 2.6f, 0f), Vector3.zero, 9.18f);
            Cyl(ctx, hub, "Antenna", "M_Base_Steel", new Vector3(0f, 6.4f, 0f), Vector3.zero, new Vector3(0.12f, 1f, 0.12f));
            Box(ctx, hub, "AirlockFrame", "M_Hazard", new Vector3(0f, 1.6f, 4.7f), Vector3.zero, new Vector3(2.2f, 2.4f, 0.6f));
            Box(ctx, hub, "AirlockDoor", "M_Base_Steel", new Vector3(0f, 1.5f, 5.0f), Vector3.zero, new Vector3(1.6f, 2.0f, 0.1f));
            CinematicBuildKit.Obj("Anchor_Airlock", hub.transform, layer, new Vector3(0f, 0f, 6.2f));

            // Módulos habitables (0°, 120°, 240°)
            string[] names = { "Module_Lab", "Module_Habitat", "Module_Systems" };
            for (int i = 0; i < 3; i++)
            {
                var module = CinematicBuildKit.Obj(names[i], root.transform, layer, Vector3.zero, new Vector3(0f, -i * 120f, 0f));
                Cyl(ctx, module, "Tunnel", "M_Base_Panel", new Vector3(6.2f, 1.8f, 0f), new Vector3(0f, 0f, 90f), new Vector3(2.2f, 1.4f, 2.2f));
                Cyl(ctx, module, "Body", "M_Base_Panel", new Vector3(12f, 2.4f, 0f), new Vector3(0f, 0f, 90f), new Vector3(4.4f, 4.5f, 4.4f));
                Dome(ctx, module, "EndCap", "M_Base_Panel", new Vector3(16.5f, 2.4f, 0f), new Vector3(0f, 0f, -90f), new Vector3(4.4f, 4.4f, 4.4f));
                foreach (float x in new[] { 8.6f, 12f, 15.4f })
                    Cyl(ctx, module, $"Band_{x:0}", "M_Base_Steel", new Vector3(x, 2.4f, 0f), new Vector3(0f, 0f, 90f), new Vector3(4.6f, 0.12f, 4.6f));
                Box(ctx, module, "WindowStrip_A", "M_Emit_Cyan", new Vector3(12f, 2.9f, 2.15f), Vector3.zero, new Vector3(5.6f, 0.22f, 0.06f), false);
                Box(ctx, module, "WindowStrip_B", "M_Emit_Cyan", new Vector3(12f, 2.9f, -2.15f), Vector3.zero, new Vector3(5.6f, 0.22f, 0.06f), false);
                Box(ctx, module, "Support_A", "M_Base_Graphite", new Vector3(10f, 0.25f, 0f), Vector3.zero, new Vector3(0.6f, 0.5f, 3.2f));
                Box(ctx, module, "Support_B", "M_Base_Graphite", new Vector3(14f, 0.25f, 0f), Vector3.zero, new Vector3(0.6f, 0.5f, 3.2f));
            }

            // Invernadero (180°)
            var greenhouse = CinematicBuildKit.Obj("Greenhouse", root.transform, layer, Vector3.zero, new Vector3(0f, 180f, 0f));
            Cyl(ctx, greenhouse, "Tunnel", "M_Base_Panel", new Vector3(7.4f, 1.6f, 0f), new Vector3(0f, 0f, 90f), new Vector3(2.2f, 2.6f, 2.2f));
            Cyl(ctx, greenhouse, "Floor", "M_Base_Graphite", new Vector3(14f, 0.05f, 0f), Vector3.zero, new Vector3(7.8f, 0.05f, 7.8f));
            var random = new System.Random(5);
            for (int i = 0; i < 9; i++)
            {
                var offset = new Vector3((float)random.NextDouble() * 4.4f - 2.2f, 0f, (float)random.NextDouble() * 4.4f - 2.2f);
                float size = 0.6f + (float)random.NextDouble() * 0.7f;
                CinematicBuildKit.Prim(PrimitiveType.Sphere, $"Plant_{i + 1}", greenhouse.transform, ctx.Mat("M_Plant"), layer,
                    new Vector3(14f, size * 0.45f, 0f) + offset, Vector3.zero, new Vector3(size, size * 0.9f, size));
            }
            Dome(ctx, greenhouse, "GlassDome", "M_Base_Glass", new Vector3(14f, 0f, 0f), Vector3.zero, new Vector3(8f, 8f, 8f), false);
            Ring(ctx, greenhouse, "Rib_A", "M_Base_Steel", new Vector3(14f, 0f, 0f), new Vector3(90f, 0f, 0f), 8.05f);
            Ring(ctx, greenhouse, "Rib_B", "M_Base_Steel", new Vector3(14f, 0f, 0f), new Vector3(90f, 90f, 0f), 8.05f);
            Ring(ctx, greenhouse, "GrowLights", "M_Emit_Green", new Vector3(14f, 2.6f, 0f), Vector3.zero, 5.4f);

            // Paneles solares (animables: clave "SolarArray")
            var solar = CinematicBuildKit.Obj("SolarArray", root.transform, layer, new Vector3(14f, 0f, -24f));
            var rows = CinematicBuildKit.Obj("SolarArray_Rows", solar.transform, layer);
            for (int r = 0; r < 2; r++)
            {
                var row = CinematicBuildKit.Obj(r == 0 ? "Row_A" : "Row_B", rows.transform, layer, new Vector3(0f, 0f, -r * 4.5f));
                for (int i = 0; i < PanelsPerRow; i++)
                    CinematicBuildKit.Nest(ctx, "PF_Prop_SolarPanel", row.transform, new Vector3(-9f + i * 3.6f, 0f, 0f), Vector3.zero, $"Panel_{i + 1:00}");
            }
            Box(ctx, solar, "JunctionBox", "M_Base_Graphite", new Vector3(-12f, 0.5f, -2.2f), Vector3.zero, new Vector3(1f, 1f, 0.8f));
            Box(ctx, solar, "OutputLight", "M_Emit_Green", new Vector3(-12f, 1.02f, -2.2f), Vector3.zero, new Vector3(0.6f, 0.04f, 0.3f), false);
            CinematicBuildKit.SetupAnimator(solar, null);
            CinematicBuildKit.AddBindingTarget(solar, "SolarArray", null);

            // Estación de energía y baterías
            var power = CinematicBuildKit.Obj("PowerStation", root.transform, layer, new Vector3(24f, 0f, 6f));
            Box(ctx, power, "Building", "M_Base_Graphite", new Vector3(0f, 1.5f, 0f), Vector3.zero, new Vector3(6f, 3f, 4f));
            for (int i = 0; i < 4; i++)
                Box(ctx, power, $"Radiator_{i + 1}", "M_Base_Steel", new Vector3(-2.2f + i * 1.45f, 3.6f, 0f), Vector3.zero, new Vector3(0.08f, 1.2f, 3.6f));
            for (int i = 0; i < 3; i++)
                Box(ctx, power, $"StatusBar_{i + 1}", "M_Emit_Green", new Vector3(-1.5f + i * 1.5f, 1.8f, -2.01f), Vector3.zero, new Vector3(0.9f, 0.12f, 0.02f), false);

            // Torre de comunicaciones (antena con barrido automático)
            var dishController = CinematicBuildKit.GetLoopController(ctx, "AC_Dish_Default", ctx.Clip("AC_Dish_Scan"));
            var tower = CinematicBuildKit.Obj("CommsTower", root.transform, layer, new Vector3(8f, 0f, 15f));
            Cyl(ctx, tower, "Mast", "M_Base_Steel", new Vector3(0f, 5f, 0f), Vector3.zero, new Vector3(0.5f, 5f, 0.5f));
            var dishPivot = CinematicBuildKit.Obj("DishPivot", tower.transform, layer, new Vector3(0f, 9.6f, 0f));
            Dome(ctx, dishPivot, "Dish", "M_Base_Panel", new Vector3(0f, 0f, 0.4f), new Vector3(-60f, 0f, 0f), new Vector3(3.2f, 1.1f, 3.2f));
            Cyl(ctx, dishPivot, "Feed", "M_Base_Graphite", new Vector3(0f, 0.6f, 0.9f), new Vector3(-60f, 0f, 0f), new Vector3(0.06f, 0.6f, 0.06f));
            CinematicBuildKit.Prim(PrimitiveType.Sphere, "Beacon", tower.transform, ctx.Mat("M_Emit_Red"), layer, new Vector3(0f, 10.3f, 0f), Vector3.zero, Vector3.one * 0.25f, false);
            CinematicBuildKit.Obj("Anchor_Transmission", tower.transform, layer, new Vector3(0f, 10.5f, 0.8f));
            CinematicBuildKit.SetupAnimator(tower, dishController);

            // Plataforma de aterrizaje
            var pad = CinematicBuildKit.Obj("LandingPad", root.transform, layer, new Vector3(30f, 0f, 24f));
            Cyl(ctx, pad, "HazardRing", "M_Hazard", new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(14.4f, 0.04f, 14.4f));
            Cyl(ctx, pad, "Surface", "M_Base_Graphite", new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(14f, 0.05f, 14f));
            Box(ctx, pad, "Mark_A", "M_Suit_Orange", new Vector3(0f, 0.115f, 0f), Vector3.zero, new Vector3(6f, 0.01f, 0.5f), false);
            Box(ctx, pad, "Mark_B", "M_Suit_Orange", new Vector3(0f, 0.115f, 0f), Vector3.zero, new Vector3(0.5f, 0.01f, 6f), false);

            // Postes de luz (en los huecos entre módulos)
            float[] postAngles = { 60f, 150f, 210f, 300f };
            for (int i = 0; i < postAngles.Length; i++)
            {
                float angle = postAngles[i] * Mathf.Deg2Rad;
                var post = CinematicBuildKit.Obj($"LightPost_{i + 1}", root.transform, layer, new Vector3(Mathf.Cos(angle) * 9f, 0f, Mathf.Sin(angle) * 9f));
                Cyl(ctx, post, "Pole", "M_Base_Steel", new Vector3(0f, 1.5f, 0f), Vector3.zero, new Vector3(0.1f, 1.5f, 0.1f));
                Box(ctx, post, "Lamp", "M_Emit_White", new Vector3(0f, 3.05f, 0f), Vector3.zero, new Vector3(0.35f, 0.1f, 0.35f), false);
            }
            return root;
        }

        // ================================================================== Interiores

        static GameObject Room(BuildContext ctx, string name, float width, float height, float depth, string wall, string floor, string ceiling)
        {
            var root = CinematicBuildKit.Obj(name, null, ctx.Layer);
            var shell = CinematicBuildKit.Obj("Shell", root.transform, ctx.Layer);
            Box(ctx, shell, "Floor", floor, new Vector3(0f, -0.1f, 0f), Vector3.zero, new Vector3(width, 0.2f, depth));
            Box(ctx, shell, "Ceiling", ceiling, new Vector3(0f, height + 0.1f, 0f), Vector3.zero, new Vector3(width, 0.2f, depth));
            Box(ctx, shell, "Wall_Back", wall, new Vector3(0f, height * 0.5f, depth * 0.5f + 0.1f), Vector3.zero, new Vector3(width + 0.4f, height, 0.2f));
            Box(ctx, shell, "Wall_Front", wall, new Vector3(0f, height * 0.5f, -depth * 0.5f - 0.1f), Vector3.zero, new Vector3(width + 0.4f, height, 0.2f));
            Box(ctx, shell, "Wall_Left", wall, new Vector3(-width * 0.5f - 0.1f, height * 0.5f, 0f), Vector3.zero, new Vector3(0.2f, height, depth));
            Box(ctx, shell, "Wall_Right", wall, new Vector3(width * 0.5f + 0.1f, height * 0.5f, 0f), Vector3.zero, new Vector3(0.2f, height, depth));
            return root;
        }

        static GameObject Group(BuildContext ctx, GameObject parent, string name, string bindingKey = null)
        {
            var group = CinematicBuildKit.Obj(name, parent.transform, ctx.Layer);
            if (!string.IsNullOrEmpty(bindingKey))
                CinematicBuildKit.AddBindingTarget(group, bindingKey, null);
            return group;
        }

        static GameObject ControlCenter(BuildContext ctx)
        {
            int layer = ctx.Layer;
            var room = Room(ctx, "ControlCenter", 12f, 4f, 9f, "M_Base_Panel", "M_Base_Floor", "M_Base_Panel");
            var details = Group(ctx, room, "Details");

            CinematicBuildKit.Prim(PrimitiveType.Quad, "Window_MarsView", details.transform, ctx.Mat("M_Window_MarsView"), layer,
                new Vector3(0f, 2.2f, 4.49f), Vector3.zero, new Vector3(6f, 2.2f, 1f), false);
            Box(ctx, details, "WindowFrame_Top", "M_Base_Steel", new Vector3(0f, 3.35f, 4.45f), Vector3.zero, new Vector3(6.3f, 0.15f, 0.12f));
            Box(ctx, details, "WindowFrame_Bottom", "M_Base_Steel", new Vector3(0f, 1.05f, 4.45f), Vector3.zero, new Vector3(6.3f, 0.15f, 0.12f));
            Box(ctx, details, "WindowFrame_Mid", "M_Base_Steel", new Vector3(0f, 2.2f, 4.45f), Vector3.zero, new Vector3(0.1f, 2.3f, 0.12f));
            for (int i = 0; i < 3; i++)
                Box(ctx, details, $"CeilingLight_{i + 1}", "M_Emit_White", new Vector3(0f, 3.97f, -2.5f + i * 2.5f), Vector3.zero, new Vector3(9f, 0.05f, 0.3f), false);
            CinematicBuildKit.Prim(PrimitiveType.Quad, "Screen_Status", details.transform, ctx.Mat("M_Screen_Data"), layer,
                new Vector3(-5.99f, 2.3f, 0f), new Vector3(0f, -90f, 0f), new Vector3(3f, 1.5f, 1f), false);

            var consoles = Group(ctx, room, "Consoles");
            CinematicBuildKit.Nest(ctx, "PF_Prop_Console", consoles.transform, new Vector3(-3.6f, 0f, 2.6f), new Vector3(0f, 15f, 0f), "Console_Left");
            CinematicBuildKit.Nest(ctx, "PF_Prop_Console", consoles.transform, new Vector3(0f, 0f, 2.9f), Vector3.zero, "Console_Center");
            CinematicBuildKit.Nest(ctx, "PF_Prop_Console", consoles.transform, new Vector3(3.6f, 0f, 2.6f), new Vector3(0f, -15f, 0f), "Console_Right");
            var comms = CinematicBuildKit.Nest(ctx, "PF_Prop_Console", consoles.transform, new Vector3(5.2f, 0f, -1.4f), new Vector3(0f, 90f, 0f), "Console_Comms");
            SetScreen(ctx, comms, "M_Screen_Mission");

            CinematicBuildKit.Nest(ctx, "PF_Prop_HoloTable", room.transform, new Vector3(0f, 0f, -1.2f), Vector3.zero, "HoloTable");

            var anchors = Group(ctx, room, "Anchors");
            CinematicBuildKit.Obj("Anchor_AURA", anchors.transform, layer, new Vector3(0f, 1.75f, -1.2f));
            CinematicBuildKit.Obj("Anchor_Astronaut_Console", anchors.transform, layer, new Vector3(0f, 0f, 1.9f));
            CinematicBuildKit.Obj("Anchor_Astronaut_Table", anchors.transform, layer, new Vector3(1.7f, 0f, -1.2f), new Vector3(0f, -90f, 0f));
            CinematicBuildKit.Obj("Anchor_Astronaut_Comms", anchors.transform, layer, new Vector3(4.2f, 0f, -1.4f), new Vector3(0f, 90f, 0f));

            var lights = Group(ctx, room, "Lights_Normal", "Lights_Normal");
            CinematicBuildKit.AddPointLight(lights.transform, "Key", layer, new Vector3(0f, 3.6f, 0f), CinematicBuildKit.Hex("#E8EFF4"), 2.2f, 12f);
            CinematicBuildKit.AddPointLight(lights.transform, "WindowFill", layer, new Vector3(0f, 2.4f, 3.4f), CinematicBuildKit.Hex("#E7B88C"), 1.2f, 6f);

            AddEmergency(ctx, room, new Vector3(0f, 3.4f, 0f), new[]
            {
                new Vector3(-5.85f, 3.4f, -3f), new Vector3(-5.85f, 3.4f, 3f), new Vector3(5.85f, 3.4f, -3f), new Vector3(5.85f, 3.4f, 3f),
            });
            return room;
        }

        static GameObject PowerRoom(BuildContext ctx)
        {
            int layer = ctx.Layer;
            var room = Room(ctx, "PowerRoom", 10f, 4.5f, 8f, "M_Base_SpaceBlue", "M_Base_Floor", "M_Base_Graphite");

            var bank = Group(ctx, room, "BatteryBank", "BatteryBank");
            CinematicBuildKit.SetupAnimator(bank, null);
            var racks = CinematicBuildKit.Obj("Racks", bank.transform, layer);
            for (int i = 0; i < BatteryRacks; i++)
            {
                bool back = i < 4;
                var position = back ? new Vector3(-3.3f + i * 2.2f, 0f, 3.55f) : new Vector3(-4.6f, 0f, -2.4f + (i - 4) * 1.6f);
                var euler = back ? Vector3.zero : new Vector3(0f, -90f, 0f);
                CinematicBuildKit.Nest(ctx, "PF_Prop_BatteryRack", racks.transform, position, euler, $"Rack_{i + 1:00}");
            }

            var machines = Group(ctx, room, "Machines");
            Cyl(ctx, machines, "Converter", "M_Base_Steel", new Vector3(3.2f, 1.6f, -1.5f), Vector3.zero, new Vector3(2.2f, 1.6f, 2.2f));
            foreach (float y in new[] { 0.8f, 1.6f, 2.4f })
                Cyl(ctx, machines, $"ConverterBand_{y:0.0}", "M_Emit_Orange", new Vector3(3.2f, y, -1.5f), Vector3.zero, new Vector3(2.28f, 0.05f, 2.28f), false);
            Cyl(ctx, machines, "ConverterCap", "M_Base_Graphite", new Vector3(3.2f, 3.25f, -1.5f), Vector3.zero, new Vector3(1.8f, 0.05f, 1.8f));
            for (int i = 0; i < 3; i++)
                Cyl(ctx, machines, $"Pipe_{i + 1}", "M_Base_Steel", new Vector3(0f, 4.1f, 1f + i * 0.4f), new Vector3(0f, 0f, 90f), new Vector3(0.24f, 5f, 0.24f));
            Cyl(ctx, machines, "Pipe_Down", "M_Base_Steel", new Vector3(3.2f, 3.7f, 0.2f), Vector3.zero, new Vector3(0.24f, 0.45f, 0.24f));
            CinematicBuildKit.Nest(ctx, "PF_Prop_Console", machines.transform, new Vector3(0.6f, 0f, -3.2f), new Vector3(0f, 180f, 0f), "Console_Power");

            var anchors = Group(ctx, room, "Anchors");
            CinematicBuildKit.Obj("Anchor_Astronaut", anchors.transform, layer, new Vector3(0f, 0f, -1.4f));
            CinematicBuildKit.Obj("Anchor_AURA", anchors.transform, layer, new Vector3(-1.6f, 1.7f, 0f));

            var lights = Group(ctx, room, "Lights_Normal", "Lights_Normal");
            CinematicBuildKit.AddPointLight(lights.transform, "Key", layer, new Vector3(0f, 4f, 0f), CinematicBuildKit.Hex("#CFE3F2"), 1.8f, 11f);
            CinematicBuildKit.AddPointLight(lights.transform, "ConverterGlow", layer, new Vector3(3.2f, 2.5f, -0.2f), CinematicBuildKit.Hex("#D88B56"), 1.5f, 5f);

            AddEmergency(ctx, room, new Vector3(0f, 3.9f, 0f), new[] { new Vector3(-4.85f, 3.9f, 1f), new Vector3(4.85f, 3.9f, 1f) });
            return room;
        }

        static GameObject Shelter(BuildContext ctx)
        {
            int layer = ctx.Layer;
            var room = Room(ctx, "Shelter", 7f, 3.2f, 6f, "M_Base_Steel", "M_Base_Floor", "M_Base_Graphite");
            var details = Group(ctx, room, "Details");

            for (int i = 0; i < 3; i++)
            {
                Box(ctx, details, $"Shield_Back_{i + 1}", "M_Base_Graphite", new Vector3(-2.2f + i * 2.2f, 1.7f, 2.88f), Vector3.zero, new Vector3(1.9f, 1.4f, 0.15f));
                Box(ctx, details, $"Shield_Left_{i + 1}", "M_Base_Graphite", new Vector3(-3.38f, 1.7f, -1.8f + i * 1.8f), Vector3.zero, new Vector3(0.15f, 1.4f, 1.6f));
            }
            Box(ctx, details, "Bunk_Lower", "M_Base_Panel", new Vector3(-2.3f, 0.45f, 1.9f), Vector3.zero, new Vector3(2f, 0.4f, 0.9f));
            Box(ctx, details, "Bunk_Upper", "M_Base_Panel", new Vector3(-2.3f, 1.55f, 1.9f), Vector3.zero, new Vector3(2f, 0.15f, 0.9f));
            Box(ctx, details, "Bunk_Mattress", "M_Fabric_Blue", new Vector3(-2.3f, 0.7f, 1.9f), Vector3.zero, new Vector3(1.9f, 0.1f, 0.8f));
            Box(ctx, details, "Bunk_Post", "M_Base_Steel", new Vector3(-1.32f, 1f, 1.48f), Vector3.zero, new Vector3(0.06f, 2f, 0.06f));
            for (int i = 0; i < 2; i++)
                Cyl(ctx, details, $"WaterTank_{i + 1}", "M_Base_Panel", new Vector3(2.4f + i * 0.9f - 0.45f, 1f, 2.3f), Vector3.zero, new Vector3(0.8f, 1f, 0.8f));
            CinematicBuildKit.Nest(ctx, "PF_Prop_Crate", details.transform, new Vector3(2.6f, 0f, 0.2f), new Vector3(0f, 10f, 0f), "Crate_1");
            CinematicBuildKit.Nest(ctx, "PF_Prop_Crate", details.transform, new Vector3(2.5f, 0.7f, 0.15f), new Vector3(0f, -5f, 0f), "Crate_2");
            CinematicBuildKit.Nest(ctx, "PF_Prop_Crate", details.transform, new Vector3(2.7f, 0f, -0.9f), new Vector3(0f, 30f, 0f), "Crate_3");
            Box(ctx, details, "DoorFrame", "M_Hazard", new Vector3(0f, 1.2f, -3.05f), Vector3.zero, new Vector3(1.7f, 2.4f, 0.25f));
            Box(ctx, details, "Door", "M_Base_Graphite", new Vector3(0f, 1.05f, -2.92f), Vector3.zero, new Vector3(1.3f, 2.1f, 0.08f));
            Box(ctx, details, "SafeStrip", "M_Emit_Green", new Vector3(0f, 2.95f, 2.85f), Vector3.zero, new Vector3(5f, 0.06f, 0.06f), false);
            Box(ctx, details, "DoorLight", "M_Emit_Green", new Vector3(0f, 2.55f, -2.9f), Vector3.zero, new Vector3(0.6f, 0.08f, 0.04f), false);

            var anchors = Group(ctx, room, "Anchors");
            CinematicBuildKit.Obj("Anchor_Astronaut", anchors.transform, layer, new Vector3(0f, 0f, 0f));
            CinematicBuildKit.Obj("Anchor_AURA", anchors.transform, layer, new Vector3(1.2f, 1.6f, 0.6f));

            var lights = Group(ctx, room, "Lights_Normal", "Lights_Normal");
            CinematicBuildKit.AddPointLight(lights.transform, "Key", layer, new Vector3(0f, 2.9f, 0f), CinematicBuildKit.Hex("#FFD9B3"), 1.6f, 8f);
            CinematicBuildKit.AddPointLight(lights.transform, "SafeGlow", layer, new Vector3(0f, 2.7f, 2.4f), CinematicBuildKit.Hex("#65C69A"), 0.8f, 4f);
            return room;
        }

        static GameObject EarthControl(BuildContext ctx)
        {
            int layer = ctx.Layer;
            var room = Room(ctx, "EarthControl", 16f, 5f, 12f, "M_Base_SpaceBlue", "M_Base_Graphite", "M_Base_SpaceBlue");
            CinematicBuildKit.AddBindingTarget(room, CinematicBindingKeys.EarthControl, null);

            var screens = Group(ctx, room, "Screens");
            Box(ctx, screens, "ScreenWall", "M_Base_Graphite", new Vector3(0f, 2.7f, 5.95f), Vector3.zero, new Vector3(15f, 4.2f, 0.1f));
            CinematicBuildKit.Prim(PrimitiveType.Quad, "Screen_Main", screens.transform, ctx.Mat("M_Screen_Mission"), layer,
                new Vector3(0f, 2.9f, 5.88f), Vector3.zero, new Vector3(8f, 3.6f, 1f), false);
            CinematicBuildKit.Prim(PrimitiveType.Quad, "Screen_Left", screens.transform, ctx.Mat("M_Screen_Data"), layer,
                new Vector3(-6f, 2.7f, 5.88f), Vector3.zero, new Vector3(3.2f, 2.2f, 1f), false);
            CinematicBuildKit.Prim(PrimitiveType.Quad, "Screen_Right", screens.transform, ctx.Mat("M_Screen_Data"), layer,
                new Vector3(6f, 2.7f, 5.88f), Vector3.zero, new Vector3(3.2f, 2.2f, 1f), false);

            var desks = Group(ctx, room, "Desks");
            Box(ctx, desks, "BackRowPlatform", "M_Base_Steel", new Vector3(0f, 0.15f, -2.2f), Vector3.zero, new Vector3(13f, 0.3f, 3.2f));
            for (int row = 0; row < 2; row++)
            {
                float z = row == 0 ? 1.6f : -1.8f;
                float y = row == 0 ? 0f : 0.3f;
                for (int i = 0; i < 4; i++)
                {
                    float x = -4.5f + i * 3f;
                    CinematicBuildKit.Nest(ctx, "PF_Prop_Console", desks.transform, new Vector3(x, y, z), Vector3.zero, $"Desk_{row + 1}_{i + 1}");
                    Operator(ctx, desks, $"Operator_{row + 1}_{i + 1}", new Vector3(x, y, z - 1.05f));
                }
            }

            var lights = Group(ctx, room, "Lights_Normal", "Lights_Normal");
            CinematicBuildKit.AddPointLight(lights.transform, "Key_Left", layer, new Vector3(-4f, 4.5f, 0f), CinematicBuildKit.Hex("#CFE3F2"), 1.5f, 10f);
            CinematicBuildKit.AddPointLight(lights.transform, "Key_Right", layer, new Vector3(4f, 4.5f, 0f), CinematicBuildKit.Hex("#CFE3F2"), 1.5f, 10f);
            CinematicBuildKit.AddPointLight(lights.transform, "ScreenGlow", layer, new Vector3(0f, 3f, 4.2f), CinematicBuildKit.Hex("#53C7D8"), 1.1f, 9f);

            var anchors = Group(ctx, room, "Anchors");
            CinematicBuildKit.Obj("Anchor_Director", anchors.transform, layer, new Vector3(0f, 0.3f, -3.4f));
            return room;
        }

        static void Operator(BuildContext ctx, GameObject parent, string name, Vector3 position)
        {
            var person = CinematicBuildKit.Obj(name, parent.transform, ctx.Layer, position);
            Box(ctx, person, "Chair", "M_Base_Graphite", new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f));
            Box(ctx, person, "ChairBack", "M_Base_Graphite", new Vector3(0f, 0.75f, -0.24f), Vector3.zero, new Vector3(0.5f, 0.6f, 0.06f));
            CinematicBuildKit.Prim(PrimitiveType.Capsule, "Body", person.transform, ctx.Mat("M_Fabric_Blue"), ctx.Layer, new Vector3(0f, 0.95f, 0.02f), new Vector3(8f, 0f, 0f), new Vector3(0.45f, 0.38f, 0.32f));
            CinematicBuildKit.Prim(PrimitiveType.Sphere, "Head", person.transform, ctx.Mat("M_Base_Graphite"), ctx.Layer, new Vector3(0f, 1.47f, 0.08f), Vector3.zero, Vector3.one * 0.22f);
            Box(ctx, person, "Headset", "M_Emit_Cyan", new Vector3(0.11f, 1.47f, 0.08f), Vector3.zero, new Vector3(0.03f, 0.08f, 0.08f), false);
        }

        static void SetScreen(BuildContext ctx, GameObject console, string material)
        {
            if (console == null)
                return;
            var screen = console.transform.Find("Screen");
            if (screen != null && screen.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.sharedMaterial = ctx.Mat(material);
        }

        static void AddEmergency(BuildContext ctx, GameObject room, Vector3 lightPosition, Vector3[] beacons)
        {
            var emergency = Group(ctx, room, "EmergencyLights", "EmergencyLights");
            CinematicBuildKit.AddPointLight(emergency.transform, "AlertLight", ctx.Layer, lightPosition, CinematicBuildKit.Hex("#DB6262"), 3f, 14f);
            for (int i = 0; i < beacons.Length; i++)
            {
                // La cúpula (+Y local) debe apuntar hacia el centro de la sala: pared izquierda → +X.
                float side = beacons[i].x < 0f ? -90f : 90f;
                CinematicBuildKit.Nest(ctx, "PF_Prop_AlertBeacon", emergency.transform, beacons[i], new Vector3(0f, 0f, side), $"Beacon_{i + 1}");
            }
            emergency.SetActive(false);
        }

        // ================================================================== Espacio

        static GameObject Planet(BuildContext ctx, string surface, string atmosphere, float diameter, float tilt)
        {
            var controller = CinematicBuildKit.GetLoopController(ctx, "AC_Planet_Default", ctx.Clip("AC_Planet_Rotate"));
            var root = CinematicBuildKit.Obj("Planet_Root", null, ctx.Layer, Vector3.zero, new Vector3(0f, 0f, tilt));
            var planet = CinematicBuildKit.Prim(PrimitiveType.Sphere, "Planet", root.transform, ctx.Mat(surface), ctx.Layer, Vector3.zero, Vector3.zero, Vector3.one * diameter);
            planet.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            CinematicBuildKit.Prim(PrimitiveType.Sphere, "Atmosphere", root.transform, ctx.Mat(atmosphere), ctx.Layer, Vector3.zero, Vector3.zero, Vector3.one * diameter * 1.025f, false);
            CinematicBuildKit.SetupAnimator(root, controller);
            return root;
        }

        static GameObject Sun(BuildContext ctx)
        {
            var root = CinematicBuildKit.Obj("Sun", null, ctx.Layer);
            CinematicBuildKit.Prim(PrimitiveType.Sphere, "Surface", root.transform, ctx.Mat("M_Sun_Surface"), ctx.Layer, Vector3.zero, Vector3.zero, Vector3.one * 40f, false);

            var corona = CinematicBuildKit.Obj("Corona", root.transform, ctx.Layer);
            var ps = Particles(corona, ctx.Mat("M_FX_Glow"), 1, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.startSize = 130f;
            main.startColor = new Color(2.2f, 1.2f, 0.5f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
            var shape = ps.shape;
            shape.enabled = false;

            var flare = CinematicBuildKit.Nest(ctx, "PF_FX_SolarFlare", root.transform, Vector3.zero, Vector3.zero, "SolarFlare");
            if (flare != null)
                CinematicBuildKit.AddBindingTarget(flare, "SolarFlare", null);
            return root;
        }
    }
}

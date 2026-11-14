using System.Collections.Generic;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Personajes provisionales construidos con primitivas y mallas procedurales (NO son modelos
    /// profesionales). Estructura pensada para sustituirlos sin tocar cinemáticas:
    ///
    /// PF_Astronaut (Animator + CinematicBindingTarget "Astronaut")
    ///   └ Visual                      ← contenedor reemplazable
    ///       └ Model (PF_Astronaut_Model, prefab anidado)
    ///           └ Hips/Spine/Chest/Neck/Head, UpperArm_L/LowerArm_L/Hand_L, UpperLeg_L/LowerLeg_L/Foot_L…
    ///
    /// Las animaciones (genéricas) apuntan a "Visual/Model/&lt;huesos&gt;": un modelo nuevo debe conservar
    /// esos nombres de hueso, o usar animaciones Humanoid propias.
    /// </summary>
    internal static class CinematicCharacterBuilder
    {
        // ------------------------------------------------------------------ Rutas de huesos (astronauta)
        const string M = "Visual/Model";
        const string Hips = M + "/Hips";
        const string Spine = Hips + "/Spine";
        const string Chest = Spine + "/Chest";
        const string Neck = Chest + "/Neck";
        const string Head = Neck + "/Head";
        const string UpperArmL = Chest + "/UpperArm_L";
        const string LowerArmL = UpperArmL + "/LowerArm_L";
        const string HandL = LowerArmL + "/Hand_L";
        const string UpperArmR = Chest + "/UpperArm_R";
        const string LowerArmR = UpperArmR + "/LowerArm_R";
        const string HandR = LowerArmR + "/Hand_R";
        const string UpperLegL = Hips + "/UpperLeg_L";
        const string LowerLegL = UpperLegL + "/LowerLeg_L";
        const string UpperLegR = Hips + "/UpperLeg_R";
        const string LowerLegR = UpperLegR + "/LowerLeg_R";

        const float HipsHeight = 0.96f;

        static readonly Dictionary<string, Vector3> AstronautRest = new Dictionary<string, Vector3>
        {
            { UpperArmL, new Vector3(0f, 0f, -10f) },
            { UpperArmR, new Vector3(0f, 0f, 10f) },
            { LowerArmL, new Vector3(-12f, 0f, 0f) },
            { LowerArmR, new Vector3(-12f, 0f, 0f) },
        };

        // ------------------------------------------------------------------ Rutas (AURA)
        const string AuraModel = "Visual/Model";
        const string AuraFloat = AuraModel + "/Float";
        const string AuraCore = AuraFloat + "/Core";
        const string AuraShell = AuraFloat + "/Shell";
        const string AuraRingA = AuraFloat + "/Ring_A";
        const string AuraRingB = AuraFloat + "/Ring_B";
        const string AuraRingC = AuraFloat + "/Ring_C";
        const string AuraOrbit = AuraFloat + "/Orbit";

        public static void BuildAll(BuildContext ctx)
        {
            BuildAstronautClips(ctx);
            BuildAuraClips(ctx);

            CinematicBuildKit.GetPrefab(ctx, PhaseBPaths.CharacterModels, "PF_Astronaut_Model", () => BuildAstronautModel(ctx));
            CinematicBuildKit.GetPrefab(ctx, PhaseBPaths.CharacterModels, "PF_AURA_Model", () => BuildAuraModel(ctx));

            var astronautController = CinematicBuildKit.GetLoopController(ctx, "AC_Astronaut_Default", ctx.Clip("AC_Astronaut_Idle"));
            var auraController = CinematicBuildKit.GetLoopController(ctx, "AC_AURA_Default", ctx.Clip("AC_AURA_Idle"));

            CinematicBuildKit.GetPrefab(ctx, PhaseBPaths.Characters, "PF_Astronaut",
                () => BuildCharacterRoot(ctx, "PF_Astronaut_Model", CinematicBindingKeys.Astronaut, astronautController));
            CinematicBuildKit.GetPrefab(ctx, PhaseBPaths.Characters, "PF_AURA",
                () => BuildCharacterRoot(ctx, "PF_AURA_Model", CinematicBindingKeys.Aura, auraController));
        }

        static GameObject BuildCharacterRoot(BuildContext ctx, string modelPrefab, string bindingKey, RuntimeAnimatorController controller)
        {
            var root = CinematicBuildKit.Obj("Root", null, ctx.Layer);
            var visual = CinematicBuildKit.Obj("Visual", root.transform, ctx.Layer);
            CinematicBuildKit.Nest(ctx, modelPrefab, visual.transform, Vector3.zero, Vector3.zero, "Model");
            CinematicBuildKit.SetupAnimator(root, controller);
            CinematicBuildKit.AddBindingTarget(root, bindingKey, visual.transform);
            return root;
        }

        // ================================================================== ASTRONAUTA

        static GameObject BuildAstronautModel(BuildContext ctx)
        {
            int layer = ctx.Layer;
            var white = ctx.Mat("M_Suit_White");
            var graphite = ctx.Mat("M_Suit_Graphite");
            var orange = ctx.Mat("M_Suit_Orange");
            var visor = ctx.Mat("M_Suit_Visor");
            var cyan = ctx.Mat("M_Emit_Cyan");
            var green = ctx.Mat("M_Emit_Green");
            var lamp = ctx.Mat("M_Emit_White");
            s_LowSphere = CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Icosphere(2, 0f, 0, false, Vector3.one), "MSH_LowSphere");

            var model = CinematicBuildKit.Obj("PF_Astronaut_Model", null, layer);
            var hips = CinematicBuildKit.Obj("Hips", model.transform, layer, new Vector3(0f, HipsHeight, 0f));
            Part(PrimitiveType.Sphere, "Mesh_Pelvis", hips, white, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.38f, 0.24f, 0.27f));
            Part(PrimitiveType.Cylinder, "Mesh_Belt", hips, graphite, new Vector3(0f, 0.07f, 0f), Vector3.zero, new Vector3(0.39f, 0.03f, 0.29f));

            var spine = CinematicBuildKit.Obj("Spine", hips.transform, layer, new Vector3(0f, 0.10f, 0f));
            var chest = CinematicBuildKit.Obj("Chest", spine.transform, layer, new Vector3(0f, 0.16f, 0f));
            Part(PrimitiveType.Capsule, "Mesh_Torso", chest, white, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.50f, 0.30f, 0.34f));
            Part(PrimitiveType.Cylinder, "Mesh_ChestStripe", chest, orange, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.505f, 0.025f, 0.345f));
            Part(PrimitiveType.Cube, "Mesh_ControlBox", chest, graphite, new Vector3(0f, 0.10f, 0.17f), Vector3.zero, new Vector3(0.22f, 0.12f, 0.06f));
            Part(PrimitiveType.Cube, "Mesh_Light_Cyan", chest, cyan, new Vector3(-0.06f, 0.12f, 0.2f), Vector3.zero, new Vector3(0.03f, 0.03f, 0.01f), false);
            Part(PrimitiveType.Cube, "Mesh_Light_Green", chest, green, new Vector3(0f, 0.12f, 0.2f), Vector3.zero, new Vector3(0.03f, 0.03f, 0.01f), false);
            Part(PrimitiveType.Cube, "Mesh_Light_Orange", chest, orange, new Vector3(0.06f, 0.12f, 0.2f), Vector3.zero, new Vector3(0.03f, 0.03f, 0.01f), false);
            Part(PrimitiveType.Cube, "Mesh_Backpack", chest, graphite, new Vector3(0f, 0.14f, -0.24f), Vector3.zero, new Vector3(0.42f, 0.50f, 0.20f));
            Part(PrimitiveType.Cube, "Mesh_BackpackStripe", chest, orange, new Vector3(0f, 0.30f, -0.24f), Vector3.zero, new Vector3(0.43f, 0.05f, 0.21f));
            Part(PrimitiveType.Cylinder, "Mesh_BackpackVent", chest, white, new Vector3(0.12f, 0.02f, -0.35f), new Vector3(90f, 0f, 0f), new Vector3(0.08f, 0.02f, 0.08f));
            Part(PrimitiveType.Sphere, "Mesh_ShoulderPad_L", chest, white, new Vector3(-0.27f, 0.25f, 0f), Vector3.zero, new Vector3(0.19f, 0.17f, 0.19f));
            Part(PrimitiveType.Sphere, "Mesh_ShoulderPad_R", chest, white, new Vector3(0.27f, 0.25f, 0f), Vector3.zero, new Vector3(0.19f, 0.17f, 0.19f));

            var neck = CinematicBuildKit.Obj("Neck", chest.transform, layer, new Vector3(0f, 0.40f, 0f));
            Part(PrimitiveType.Cylinder, "Mesh_NeckRing", neck, graphite, Vector3.zero, Vector3.zero, new Vector3(0.27f, 0.03f, 0.27f));
            var head = CinematicBuildKit.Obj("Head", neck.transform, layer, new Vector3(0f, 0.06f, 0f));
            Part(PrimitiveType.Sphere, "Mesh_Helmet", head, white, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.38f, 0.38f, 0.38f));
            Part(PrimitiveType.Sphere, "Mesh_Visor", head, visor, new Vector3(0f, 0.12f, 0.075f), Vector3.zero, new Vector3(0.31f, 0.25f, 0.24f));
            Part(PrimitiveType.Cube, "Mesh_HelmetLamp", head, lamp, new Vector3(0.17f, 0.22f, 0.05f), Vector3.zero, new Vector3(0.04f, 0.04f, 0.06f), false);
            Part(PrimitiveType.Cylinder, "Mesh_HelmetStripe", head, orange, new Vector3(0f, 0.30f, 0f), Vector3.zero, new Vector3(0.12f, 0.012f, 0.12f));

            BuildArm(chest.transform, "L", -1f, white, graphite, orange, cyan, layer);
            BuildArm(chest.transform, "R", 1f, white, graphite, orange, cyan, layer);
            BuildLeg(hips.transform, "L", -1f, white, graphite, orange, layer);
            BuildLeg(hips.transform, "R", 1f, white, graphite, orange, layer);
            return model;
        }

        static void BuildArm(Transform chest, string side, float sign, Material white, Material graphite, Material orange, Material cyan, int layer)
        {
            var upper = CinematicBuildKit.Obj("UpperArm_" + side, chest, layer, new Vector3(0.29f * sign, 0.22f, 0f), new Vector3(0f, 0f, 10f * sign));
            Part(PrimitiveType.Capsule, "Mesh_UpperArm", upper, white, new Vector3(0f, -0.15f, 0f), Vector3.zero, new Vector3(0.15f, 0.17f, 0.15f));
            Part(PrimitiveType.Cylinder, "Mesh_ArmBand", upper, orange, new Vector3(0f, -0.06f, 0f), Vector3.zero, new Vector3(0.155f, 0.02f, 0.155f));
            Part(PrimitiveType.Sphere, "Mesh_Elbow", upper, graphite, new Vector3(0f, -0.30f, 0f), Vector3.zero, new Vector3(0.13f, 0.13f, 0.13f));

            var lower = CinematicBuildKit.Obj("LowerArm_" + side, upper.transform, layer, new Vector3(0f, -0.31f, 0f), new Vector3(-12f, 0f, 0f));
            Part(PrimitiveType.Capsule, "Mesh_LowerArm", lower, white, new Vector3(0f, -0.14f, 0f), Vector3.zero, new Vector3(0.13f, 0.15f, 0.13f));
            if (side == "L")
                Part(PrimitiveType.Cube, "Mesh_WristDisplay", lower, cyan, new Vector3(0f, -0.20f, 0.065f), Vector3.zero, new Vector3(0.07f, 0.09f, 0.015f), false);

            var hand = CinematicBuildKit.Obj("Hand_" + side, lower.transform, layer, new Vector3(0f, -0.29f, 0f));
            Part(PrimitiveType.Sphere, "Mesh_Glove", hand, graphite, new Vector3(0f, -0.05f, 0f), Vector3.zero, new Vector3(0.11f, 0.14f, 0.09f));
        }

        static void BuildLeg(Transform hips, string side, float sign, Material white, Material graphite, Material orange, int layer)
        {
            var upper = CinematicBuildKit.Obj("UpperLeg_" + side, hips, layer, new Vector3(0.12f * sign, -0.06f, 0f));
            Part(PrimitiveType.Capsule, "Mesh_UpperLeg", upper, white, new Vector3(0f, -0.21f, 0f), Vector3.zero, new Vector3(0.19f, 0.22f, 0.19f));
            Part(PrimitiveType.Sphere, "Mesh_Knee", upper, graphite, new Vector3(0f, -0.42f, 0.01f), Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f));

            var lower = CinematicBuildKit.Obj("LowerLeg_" + side, upper.transform, layer, new Vector3(0f, -0.43f, 0f));
            Part(PrimitiveType.Capsule, "Mesh_LowerLeg", lower, white, new Vector3(0f, -0.20f, 0f), Vector3.zero, new Vector3(0.17f, 0.20f, 0.17f));
            Part(PrimitiveType.Cylinder, "Mesh_LegBand", lower, orange, new Vector3(0f, -0.12f, 0f), Vector3.zero, new Vector3(0.175f, 0.02f, 0.175f));

            var foot = CinematicBuildKit.Obj("Foot_" + side, lower.transform, layer, new Vector3(0f, -0.40f, 0f));
            Part(PrimitiveType.Cube, "Mesh_Boot", foot, graphite, new Vector3(0f, -0.02f, 0.04f), Vector3.zero, new Vector3(0.18f, 0.12f, 0.30f));
        }

        /// <summary>Esfera de 320 triángulos (la primitiva de Unity tiene 768): optimización de la Fase F.</summary>
        static Mesh s_LowSphere;

        static void Part(PrimitiveType type, string name, GameObject parent, Material material, Vector3 position, Vector3 euler, Vector3 scale, bool shadows = true)
        {
            if (type == PrimitiveType.Sphere && s_LowSphere != null)
                CinematicBuildKit.MeshObj(name, parent.transform, s_LowSphere, material, parent.layer, position, euler, scale, shadows);
            else
                CinematicBuildKit.Prim(type, name, parent.transform, material, parent.layer, position, euler, scale, shadows);
        }

        static Dictionary<string, Vector3> P(params (string bone, Vector3 euler)[] entries)
        {
            var pose = new Dictionary<string, Vector3>(AstronautRest);
            foreach (var entry in entries)
                pose[entry.bone] = entry.euler;
            return pose;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static void BuildAstronautClips(BuildContext ctx)
        {
            string folder = PhaseBPaths.AnimAstronaut;
            var rest = AstronautRest;

            // Reposo con respiración (4 s, bucle)
            CinematicBuildKit.SaveClip(ctx, new PoseClipBuilder("AC_Astronaut_Idle", true, rest)
                .Key(0f, P((Chest, V(0, 0, 0)), (Head, V(0, 0, 0))))
                .Key(2f, P((Chest, V(-2, 0, 0)), (Head, V(2, 4, 0)), (UpperArmL, V(0, 0, -12)), (UpperArmR, V(0, 0, 12))))
                .Key(4f, P((Chest, V(0, 0, 0)), (Head, V(0, 0, 0))))
                .Height(Hips, PoseClipBuilder.Keys(new[] { 0f, 2f, 4f }, new[] { HipsHeight, HipsHeight - 0.01f, HipsHeight }))
                .Build(), folder);

            // Caminar con baja gravedad (1,4 s, bucle)
            float[] walkTimes = { 0f, 0.35f, 0.7f, 1.05f, 1.4f };
            float[] legL = { -22f, 0f, 22f, 0f, -22f };
            float[] kneeL = { 5f, 10f, 15f, 45f, 5f };
            float[] kneeR = { 15f, 45f, 5f, 10f, 15f };
            float[] arm = { 15f, 0f, -15f, 0f, 15f };
            float[] twist = { 4f, 0f, -4f, 0f, 4f };
            var walk = new PoseClipBuilder("AC_Astronaut_Walk", true, rest);
            for (int i = 0; i < walkTimes.Length; i++)
            {
                walk.Key(walkTimes[i], P(
                    (UpperLegL, V(legL[i], 0, 0)), (UpperLegR, V(-legL[i], 0, 0)),
                    (LowerLegL, V(kneeL[i], 0, 0)), (LowerLegR, V(kneeR[i], 0, 0)),
                    (UpperArmL, V(arm[i], 0, -10)), (UpperArmR, V(-arm[i], 0, 10)),
                    (Chest, V(-3, twist[i], 0))));
            }
            walk.Height(Hips, PoseClipBuilder.Keys(walkTimes, new[] { HipsHeight - 0.03f, HipsHeight, HipsHeight - 0.03f, HipsHeight, HipsHeight - 0.03f }));
            CinematicBuildKit.SaveClip(ctx, walk.Build(), folder);

            // Mirar alrededor (5 s)
            CinematicBuildKit.SaveClip(ctx, new PoseClipBuilder("AC_Astronaut_LookAround", false, rest)
                .Key(0f, P())
                .Key(1.2f, P((Head, V(5, -40, 0)), (Chest, V(0, -10, 0))))
                .Key(2.4f, P((Head, V(5, -40, 0)), (Chest, V(0, -10, 0))))
                .Key(3.6f, P((Head, V(0, 35, 0)), (Chest, V(0, 8, 0))))
                .Key(5f, P())
                .Build(), folder);

            // Señalar / explicar (3 s)
            CinematicBuildKit.SaveClip(ctx, new PoseClipBuilder("AC_Astronaut_Gesture", false, rest)
                .Key(0f, P())
                .Key(0.6f, P((UpperArmR, V(-75, 0, 5)), (LowerArmR, V(-10, 0, 0)), (Head, V(0, 10, 0)), (Chest, V(0, 8, 0))))
                .Key(2.2f, P((UpperArmR, V(-75, 0, 5)), (LowerArmR, V(-10, 0, 0)), (Head, V(0, 10, 0)), (Chest, V(0, 8, 0))))
                .Key(3f, P())
                .Build(), folder);

            // Trabajar en una consola (3 s, bucle)
            var console = new PoseClipBuilder("AC_Astronaut_WorkConsole", true, rest);
            for (int i = 0; i <= 6; i++)
            {
                float t = i * 0.5f;
                bool even = i % 2 == 0;
                console.Key(t, P(
                    (UpperArmL, V(-50, 0, -8)), (UpperArmR, V(-50, 0, 8)),
                    (LowerArmL, V(-50, 0, 0)), (LowerArmR, V(-50, 0, 0)),
                    (HandL, V(even ? 0 : 12, 0, 0)), (HandR, V(even ? 12 : 0, 0, 0)),
                    (Head, V(18, 0, 0)), (Chest, V(6, 0, 0))));
            }
            CinematicBuildKit.SaveClip(ctx, console.Build(), folder);

            // Consultar el panel de muñeca (3 s)
            CinematicBuildKit.SaveClip(ctx, new PoseClipBuilder("AC_Astronaut_CheckWrist", false, rest)
                .Key(0f, P())
                .Key(0.6f, P((UpperArmL, V(-55, 0, 25)), (LowerArmL, V(-100, 0, 0)), (Head, V(25, -15, 0))))
                .Key(2.2f, P((UpperArmL, V(-55, 0, 25)), (LowerArmL, V(-100, 0, 0)), (Head, V(25, -15, 0))))
                .Key(3f, P())
                .Build(), folder);

            // Celebrar (2,5 s)
            CinematicBuildKit.SaveClip(ctx, new PoseClipBuilder("AC_Astronaut_Celebrate", false, rest)
                .Key(0f, P())
                .Key(0.5f, P((UpperArmR, V(-150, 0, 5)), (LowerArmR, V(-30, 0, 0)), (Chest, V(-5, 0, 0)), (Head, V(-10, 0, 0))))
                .Key(0.9f, P((UpperArmR, V(-140, 0, 5)), (LowerArmR, V(-20, 0, 0)), (Chest, V(-5, 0, 0)), (Head, V(-10, 0, 0))))
                .Key(1.3f, P((UpperArmR, V(-150, 0, 5)), (LowerArmR, V(-30, 0, 0)), (Chest, V(-5, 0, 0)), (Head, V(-10, 0, 0))))
                .Key(2.5f, P())
                .Build(), folder);

            // Preocupación (3 s, termina en la pose)
            CinematicBuildKit.SaveClip(ctx, new PoseClipBuilder("AC_Astronaut_Concern", false, rest)
                .Key(0f, P())
                .Key(1f, P((Chest, V(10, 0, 0)), (Head, V(25, 0, 0)), (UpperArmL, V(0, 0, -5)), (UpperArmR, V(0, 0, 5))))
                .Key(3f, P((Chest, V(10, 0, 0)), (Head, V(28, -5, 0)), (UpperArmL, V(0, 0, -5)), (UpperArmR, V(0, 0, 5))))
                .Build(), folder);
        }

        // ================================================================== AURA

        static GameObject BuildAuraModel(BuildContext ctx)
        {
            int layer = ctx.Layer;
            var coreMesh = CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Icosphere(1, 0f, 0, true, Vector3.one), "MSH_AURA_Core");
            var shellMesh = CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Icosphere(0, 0f, 0, true, Vector3.one), "MSH_AURA_Shell");
            var ringA = CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Torus(0.50f, 0.010f, 64, 6), "MSH_AURA_RingA");
            var ringB = CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Torus(0.62f, 0.008f, 64, 6), "MSH_AURA_RingB");
            var ringC = CinematicBuildKit.SaveMesh(ctx, CinematicMeshFactory.Torus(0.75f, 0.006f, 72, 6), "MSH_AURA_RingC");

            var core = ctx.Mat("M_AURA_Core");
            var holo = ctx.Mat("M_Holo_Cyan");
            var node = ctx.Mat("M_Emit_Cyan");

            var model = CinematicBuildKit.Obj("PF_AURA_Model", null, layer);
            var floating = CinematicBuildKit.Obj("Float", model.transform, layer);
            CinematicBuildKit.MeshObj("Core", floating.transform, coreMesh, core, layer, Vector3.zero, Vector3.zero, Vector3.one * 0.44f, false);
            CinematicBuildKit.MeshObj("Shell", floating.transform, shellMesh, holo, layer, Vector3.zero, new Vector3(0f, 30f, 0f), Vector3.one * 0.72f, false);
            CinematicBuildKit.MeshObj("Ring_A", floating.transform, ringA, holo, layer, Vector3.zero, new Vector3(70f, 0f, 0f), Vector3.one, false);
            CinematicBuildKit.MeshObj("Ring_B", floating.transform, ringB, holo, layer, Vector3.zero, new Vector3(-40f, 0f, 30f), Vector3.one, false);
            CinematicBuildKit.MeshObj("Ring_C", floating.transform, ringC, holo, layer, Vector3.zero, new Vector3(15f, 0f, -60f), Vector3.one, false);
            var orbit = CinematicBuildKit.Obj("Orbit", floating.transform, layer);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                CinematicBuildKit.Prim(PrimitiveType.Sphere, $"Node_{i + 1}", orbit.transform, node, layer,
                    new Vector3(Mathf.Cos(angle) * 0.62f, (i - 1) * 0.08f, Mathf.Sin(angle) * 0.62f), Vector3.zero, Vector3.one * 0.05f, false);
            }
            return model;
        }

        static void BuildAuraClips(BuildContext ctx)
        {
            string folder = PhaseBPaths.AnimAura;
            CinematicBuildKit.SaveClip(ctx, AuraLoop("AC_AURA_Idle", 6f, 0f, 1f, null), folder);
            CinematicBuildKit.SaveClip(ctx, AuraLoop("AC_AURA_Speak", 6f, 0.4f, 1f, null), folder);
            CinematicBuildKit.SaveClip(ctx, AuraLoop("AC_AURA_Alert", 6f, 0.25f, 1.6f, CinematicBuildKit.Hex("#DB6262")), folder);

            // Aparición: escala desde cero con los anillos girando rápido (2 s).
            var appear = new PoseClipBuilder("AC_AURA_Appear", false, new Dictionary<string, Vector3>());
            appear.Curve(AuraModel, typeof(Transform), "m_LocalScale.x", PoseClipBuilder.Keys(new[] { 0f, 1.3f, 2f }, new[] { 0f, 1.12f, 1f }));
            appear.Curve(AuraModel, typeof(Transform), "m_LocalScale.y", PoseClipBuilder.Keys(new[] { 0f, 0.6f, 1.3f, 2f }, new[] { 0f, 0.3f, 1.12f, 1f }));
            appear.Curve(AuraModel, typeof(Transform), "m_LocalScale.z", PoseClipBuilder.Keys(new[] { 0f, 1.3f, 2f }, new[] { 0f, 1.12f, 1f }));
            AddSpin(appear, AuraRingA, 2f, new Vector3(70f, 0f, 0f), 1, 540f);
            AddSpin(appear, AuraRingB, 2f, new Vector3(-40f, 0f, 30f), 1, -540f);
            AddSpin(appear, AuraRingC, 2f, new Vector3(15f, 0f, -60f), 1, 540f);
            CinematicBuildKit.SaveClip(ctx, appear.Build(), folder);
        }

        /// <summary>Bucle de AURA: flotación, anillos en precesión, núcleo latiendo; pulso = ritmo de habla.</summary>
        static AnimationClip AuraLoop(string name, float length, float pulsePeriod, float speed, Color? tint)
        {
            var builder = new PoseClipBuilder(name, true, new Dictionary<string, Vector3>());
            builder.Height(AuraFloat, PoseClipBuilder.Keys(new[] { 0f, length * 0.5f, length }, new[] { 0f, 0.05f, 0f }));

            AddSpin(builder, AuraRingA, length, new Vector3(70f, 0f, 0f), 1, 360f * speed);
            AddSpin(builder, AuraRingB, length, new Vector3(-40f, 0f, 30f), 1, -360f * speed);
            AddSpin(builder, AuraRingC, length, new Vector3(15f, 0f, -60f), 1, 360f * speed);
            AddSpin(builder, AuraOrbit, length, Vector3.zero, 1, 360f * speed);
            AddSpin(builder, AuraCore, length, Vector3.zero, 1, 120f * speed);

            var times = new List<float>();
            var coreScale = new List<float>();
            var shellScale = new List<float>();
            if (pulsePeriod <= 0f)
            {
                times.AddRange(new[] { 0f, length * 0.5f, length });
                coreScale.AddRange(new[] { 0.44f, 0.465f, 0.44f });
                shellScale.AddRange(new[] { 0.72f, 0.74f, 0.72f });
            }
            else
            {
                int steps = Mathf.RoundToInt(length / pulsePeriod);
                for (int i = 0; i <= steps; i++)
                {
                    bool peak = i % 2 == 1;
                    times.Add(i * length / steps);
                    float amount = peak ? (i % 4 == 1 ? 1.14f : 1.08f) : 1f;
                    coreScale.Add(0.44f * amount);
                    shellScale.Add(0.72f * (peak ? 1.05f : 1f));
                }
            }

            foreach (var axis in new[] { "x", "y", "z" })
            {
                builder.Curve(AuraCore, typeof(Transform), "m_LocalScale." + axis, PoseClipBuilder.Keys(times.ToArray(), coreScale.ToArray()));
                builder.Curve(AuraShell, typeof(Transform), "m_LocalScale." + axis, PoseClipBuilder.Keys(times.ToArray(), shellScale.ToArray()));
            }

            if (tint.HasValue)
            {
                var color = tint.Value;
                AddColor(builder, AuraCore, "material._EmissionColor", color * 2.6f, length);
                AddColor(builder, AuraCore, "material._BaseColor", color * 0.6f, length);
                var shell = color * 0.9f;
                shell.a = 0.45f;
                AddColor(builder, AuraShell, "material._BaseColor", shell, length);
            }
            return builder.Build();
        }

        static void AddSpin(PoseClipBuilder builder, string path, float length, Vector3 baseEuler, int axis, float degrees)
        {
            builder.Curve(path, typeof(Transform), "localEulerAnglesRaw.x",
                axis == 0 ? PoseClipBuilder.Linear(length, baseEuler.x, baseEuler.x + degrees) : PoseClipBuilder.Constant(length, baseEuler.x));
            builder.Curve(path, typeof(Transform), "localEulerAnglesRaw.y",
                axis == 1 ? PoseClipBuilder.Linear(length, baseEuler.y, baseEuler.y + degrees) : PoseClipBuilder.Constant(length, baseEuler.y));
            builder.Curve(path, typeof(Transform), "localEulerAnglesRaw.z",
                axis == 2 ? PoseClipBuilder.Linear(length, baseEuler.z, baseEuler.z + degrees) : PoseClipBuilder.Constant(length, baseEuler.z));
        }

        static void AddColor(PoseClipBuilder builder, string path, string property, Color color, float length)
        {
            builder.Curve(path, typeof(MeshRenderer), property + ".r", PoseClipBuilder.Constant(length, color.r));
            builder.Curve(path, typeof(MeshRenderer), property + ".g", PoseClipBuilder.Constant(length, color.g));
            builder.Curve(path, typeof(MeshRenderer), property + ".b", PoseClipBuilder.Constant(length, color.b));
            builder.Curve(path, typeof(MeshRenderer), property + ".a", PoseClipBuilder.Constant(length, color.a));
        }
    }
}

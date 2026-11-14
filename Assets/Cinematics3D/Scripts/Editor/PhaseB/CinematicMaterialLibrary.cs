using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Biblioteca de materiales de las cinemáticas, basada en la paleta del proyecto.
    /// Los modelos solo referencian estos materiales: cambiar el aspecto de algo (Nivel 1)
    /// es editar un material de Assets/Cinematics3D/Materials.
    /// </summary>
    internal static class CinematicMaterialLibrary
    {
        const string Lit = "Universal Render Pipeline/Lit";
        const string Unlit = "Universal Render Pipeline/Unlit";
        const string ParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";

        public static void BuildAll(BuildContext ctx)
        {
            string folder = PhaseBPaths.Materials;
            Color H(string hex) => CinematicBuildKit.Hex(hex);

            // Traje del astronauta
            LitMat(ctx, "M_Suit_White", H("#E8EFF4"), 0.05f, 0.45f);
            LitMat(ctx, "M_Suit_Graphite", H("#2B2F36"), 0.1f, 0.35f);
            LitMat(ctx, "M_Suit_Orange", H("#D88B56"), 0f, 0.4f);
            LitMat(ctx, "M_Suit_Visor", H("#0A0F16"), 0.85f, 0.95f);

            // Base Aurora
            LitMat(ctx, "M_Base_Panel", Color.white, 0.05f, 0.4f, "T_Base_Panel", new Vector2(2f, 2f), H("#E8EFF4"));
            LitMat(ctx, "M_Base_Steel", H("#30465D"), 0.45f, 0.55f);
            LitMat(ctx, "M_Base_Graphite", H("#22262D"), 0.1f, 0.3f);
            LitMat(ctx, "M_Base_Floor", Color.white, 0.2f, 0.25f, "T_Base_Panel", new Vector2(4f, 4f), H("#4A525C"));
            LitMat(ctx, "M_Base_SpaceBlue", H("#18283A"), 0.2f, 0.4f);
            LitMat(ctx, "M_SolarPanel", Color.white, 0.3f, 0.85f, "T_Solar_Cells", new Vector2(3f, 2f), Color.white);
            LitMat(ctx, "M_Hazard", Color.white, 0f, 0.3f, "T_Hazard_Stripes", new Vector2(4f, 1f), Color.white);
            LitMat(ctx, "M_Plant", H("#65C69A"), 0f, 0.3f);
            LitMat(ctx, "M_Fabric_Blue", H("#355C7D"), 0f, 0.2f);
            CinematicBuildKit.GetMaterial(ctx, folder, "M_Base_Glass", Lit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.62f, 0.89f, 0.93f, 0.22f));
                m.SetFloat("_Smoothness", 0.95f);
                m.SetFloat("_Metallic", 0.1f);
                CinematicBuildKit.MakeTransparent(m, false, false);
            });

            // Marte, la Tierra y el Sol
            LitMat(ctx, "M_Mars_Ground", Color.white, 0f, 0.12f, "T_Mars_Ground", Vector2.one, Color.white);
            LitMat(ctx, "M_Mars_Rock", H("#7A4129"), 0f, 0.12f);
            LitMat(ctx, "M_Mars_Planet", Color.white, 0f, 0.05f, "T_Mars_Planet", Vector2.one, Color.white);
            LitMat(ctx, "M_Earth_Planet", Color.white, 0f, 0.35f, "T_Earth_Planet", Vector2.one, Color.white);
            UnlitMat(ctx, "M_Sun_Surface", "T_Sun_Surface", new Color(4f, 2.8f, 1.6f, 1f));

            // Emisivos (luces, indicadores)
            EmissiveMat(ctx, "M_Emit_Cyan", H("#53C7D8"), 3f);
            EmissiveMat(ctx, "M_Emit_Green", H("#65C69A"), 2.5f);
            EmissiveMat(ctx, "M_Emit_Red", H("#DB6262"), 3f);
            EmissiveMat(ctx, "M_Emit_White", H("#E8EFF4"), 2f);
            EmissiveMat(ctx, "M_Emit_Orange", H("#D88B56"), 2.5f);
            EmissiveMat(ctx, "M_AURA_Core", H("#53C7D8"), 2.4f);

            // Pantallas
            UnlitMat(ctx, "M_Screen_Data", "T_Screen_Data", new Color(1.3f, 1.3f, 1.3f, 1f));
            UnlitMat(ctx, "M_Screen_Alert", "T_Screen_Alert", new Color(1.4f, 1.4f, 1.4f, 1f));
            UnlitMat(ctx, "M_Screen_Mission", "T_Screen_Mission", new Color(1.3f, 1.3f, 1.3f, 1f));
            UnlitMat(ctx, "M_Window_MarsView", "T_Window_MarsView", Color.white);

            // Hologramas y atmósferas (transparentes aditivos)
            AdditiveMat(ctx, "M_Holo_Cyan", new Color(0.33f, 0.78f, 0.85f, 0.45f), true);
            AdditiveMat(ctx, "M_Holo_Beam", new Color(0.33f, 0.78f, 0.85f, 0.14f), true);
            AdditiveMat(ctx, "M_Atmo_Mars", new Color(0.85f, 0.55f, 0.34f, 0.12f), false);
            AdditiveMat(ctx, "M_Atmo_Earth", new Color(0.44f, 0.72f, 1f, 0.2f), false);

            // Partículas
            ParticleMat(ctx, "M_FX_Dust", new Color(0.85f, 0.55f, 0.34f, 0.18f), false);
            ParticleMat(ctx, "M_FX_Glow", new Color(1f, 1f, 1f, 1f), true);

            // Cielos
            ProceduralSky(ctx, "M_Sky_MarsDay", new Color(0.86f, 0.58f, 0.40f), new Color(0.32f, 0.20f, 0.14f), 1.7f, 1.05f);
            ProceduralSky(ctx, "M_Sky_MarsDusk", new Color(0.35f, 0.52f, 0.78f), new Color(0.22f, 0.13f, 0.10f), 2.4f, 0.55f);
            ProceduralSky(ctx, "M_Sky_MarsNight", new Color(0.20f, 0.22f, 0.32f), new Color(0.06f, 0.04f, 0.04f), 1.2f, 0.12f);
            CinematicBuildKit.GetMaterial(ctx, PhaseBPaths.Skies, "M_Sky_Space", "Skybox/Panoramic", m =>
            {
                if (ctx.Textures.TryGetValue("T_Starfield", out var stars))
                    m.SetTexture("_MainTex", stars);
                m.SetFloat("_Exposure", 1.1f);
                m.SetFloat("_Mapping", 1f);
                m.SetFloat("_ImageType", 0f);
                m.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            });
        }

        static void LitMat(BuildContext ctx, string name, Color color, float metallic, float smoothness,
            string texture = null, Vector2 tiling = default, Color tint = default)
        {
            CinematicBuildKit.GetMaterial(ctx, PhaseBPaths.Materials, name, Lit, m =>
            {
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smoothness);
                if (texture != null && ctx.Textures.TryGetValue(texture, out var map))
                {
                    m.SetTexture("_BaseMap", map);
                    m.SetTextureScale("_BaseMap", tiling == default ? Vector2.one : tiling);
                    m.SetColor("_BaseColor", tint == default ? Color.white : tint);
                }
                else
                {
                    m.SetColor("_BaseColor", color);
                }
            });
        }

        static void EmissiveMat(BuildContext ctx, string name, Color color, float intensity)
        {
            CinematicBuildKit.GetMaterial(ctx, PhaseBPaths.Materials, name, Lit, m =>
            {
                m.SetColor("_BaseColor", color * 0.6f);
                m.SetFloat("_Smoothness", 0.6f);
                CinematicBuildKit.SetEmission(m, color, intensity);
            });
        }

        static void UnlitMat(BuildContext ctx, string name, string texture, Color color)
        {
            CinematicBuildKit.GetMaterial(ctx, PhaseBPaths.Materials, name, Unlit, m =>
            {
                if (ctx.Textures.TryGetValue(texture, out var map))
                    m.SetTexture("_BaseMap", map);
                m.SetColor("_BaseColor", color);
            });
        }

        static void AdditiveMat(BuildContext ctx, string name, Color color, bool doubleSided)
        {
            CinematicBuildKit.GetMaterial(ctx, PhaseBPaths.Materials, name, Unlit, m =>
            {
                m.SetColor("_BaseColor", color);
                CinematicBuildKit.MakeTransparent(m, true, doubleSided);
            });
        }

        static void ParticleMat(BuildContext ctx, string name, Color color, bool additive)
        {
            CinematicBuildKit.GetMaterial(ctx, PhaseBPaths.Materials, name, ParticlesUnlit, m =>
            {
                if (ctx.Textures.TryGetValue("T_FX_SoftDot", out var dot))
                    m.SetTexture("_BaseMap", dot);
                m.SetColor("_BaseColor", color);
                CinematicBuildKit.MakeTransparent(m, additive, true);
            });
        }

        static void ProceduralSky(BuildContext ctx, string name, Color skyTint, Color ground, float atmosphere, float exposure)
        {
            CinematicBuildKit.GetMaterial(ctx, PhaseBPaths.Skies, name, "Skybox/Procedural", m =>
            {
                m.EnableKeyword("_SUNDISK_HIGH_QUALITY");
                m.SetFloat("_SunDisk", 2f);
                m.SetFloat("_SunSize", 0.025f);
                m.SetFloat("_SunSizeConvergence", 6f);
                m.SetFloat("_AtmosphereThickness", atmosphere);
                m.SetColor("_SkyTint", skyTint);
                m.SetColor("_GroundColor", ground);
                m.SetFloat("_Exposure", exposure);
            });
        }
    }
}

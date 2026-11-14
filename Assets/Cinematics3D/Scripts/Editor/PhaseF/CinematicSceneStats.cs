using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>Presupuesto de las cinemáticas para Android de gama media (se comprueba en pruebas e informe).</summary>
    public static class CinematicAndroidBudget
    {
        /// <summary>Triángulos de toda la escena (incluye todas sus "estaciones"; en cada plano se ve solo una parte).</summary>
        public const long MaxSceneTriangles = 500_000;
        public const long MaxAstronautTriangles = 15_000;
        public const long MaxAuraTriangles = 5_000;
        public const int MaxPointLights = 24;
        public const int MaxShadowedAdditionalLights = 0;
        public const int MaxParticles = 1_500;
        public const float MaxTextureMegabytes = 64f;
        public const int MaxMaterials = 80;
    }

    /// <summary>Mediciones de una escena cinemática abierta en el Editor.</summary>
    public sealed class CinematicSceneStats
    {
        public string SceneName;
        public int Renderers;
        public long Triangles;
        public int Materials;
        public int Textures;
        public float TextureMegabytes;
        public int PointLights;
        public int ShadowedAdditionalLights;
        public int ParticleSystems;
        public int MaxParticles;
        public int AudioSources;
        public int VirtualCameras;
        public double Duration;

        public static long CountTriangles(GameObject root)
        {
            long triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                triangles += MeshTriangles(filter.sharedMesh);
            return triangles;
        }

        static long MeshTriangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    count += mesh.GetIndexCount(i) / 3;
            }
            return count;
        }

        public static CinematicSceneStats Measure(Scene scene)
        {
            var stats = new CinematicSceneStats { SceneName = scene.name };
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            var textureIds = new List<int>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    stats.Renderers++;
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || !materials.Add(material))
                            continue;
                        material.GetTexturePropertyNameIDs(textureIds);
                        foreach (int id in textureIds)
                        {
                            var texture = material.GetTexture(id);
                            if (texture != null)
                                textures.Add(texture);
                        }
                    }
                }

                stats.Triangles += CountTriangles(root);

                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional)
                        continue;
                    stats.PointLights++;
                    if (light.shadows != LightShadows.None)
                        stats.ShadowedAdditionalLights++;
                }

                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    stats.ParticleSystems++;
                    stats.MaxParticles += particles.main.maxParticles;
                }

                stats.AudioSources += root.GetComponentsInChildren<AudioSource>(true).Length;

                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Name == "CinemachineCamera")
                        stats.VirtualCameras++;
                }

                var sceneRoot = root.GetComponentInChildren<CinematicSceneRoot>(true);
                if (sceneRoot != null && sceneRoot.Director != null && sceneRoot.Director.playableAsset != null)
                    stats.Duration = sceneRoot.Director.playableAsset.duration;
            }

            stats.Materials = materials.Count;
            stats.Textures = textures.Count;
            foreach (var texture in textures)
            {
                // Estimación en Android: ASTC 6x6 ≈ 0,9 bytes/píxel + 33 % de mipmaps.
                stats.TextureMegabytes += texture.width * texture.height * 0.9f * 1.33f / (1024f * 1024f);
            }
            return stats;
        }

        public IEnumerable<string> BudgetProblems()
        {
            if (Triangles > CinematicAndroidBudget.MaxSceneTriangles)
                yield return $"{Triangles:N0} triángulos (máx. {CinematicAndroidBudget.MaxSceneTriangles:N0})";
            if (PointLights > CinematicAndroidBudget.MaxPointLights)
                yield return $"{PointLights} luces puntuales/foco (máx. {CinematicAndroidBudget.MaxPointLights})";
            if (ShadowedAdditionalLights > CinematicAndroidBudget.MaxShadowedAdditionalLights)
                yield return $"{ShadowedAdditionalLights} luces adicionales con sombra (máx. {CinematicAndroidBudget.MaxShadowedAdditionalLights})";
            if (MaxParticles > CinematicAndroidBudget.MaxParticles)
                yield return $"{MaxParticles} partículas máximas (máx. {CinematicAndroidBudget.MaxParticles})";
            if (TextureMegabytes > CinematicAndroidBudget.MaxTextureMegabytes)
                yield return $"{TextureMegabytes:0.0} MB de texturas estimados (máx. {CinematicAndroidBudget.MaxTextureMegabytes})";
            if (Materials > CinematicAndroidBudget.MaxMaterials)
                yield return $"{Materials} materiales (máx. {CinematicAndroidBudget.MaxMaterials})";
        }

        public string ToRow()
        {
            var builder = new StringBuilder();
            builder.Append($"| {SceneName} | {Duration:0.0} s | {VirtualCameras} | {Triangles:N0} | {Renderers} | {Materials} | ");
            builder.Append($"{Textures} ({TextureMegabytes:0.0} MB) | {PointLights} | {MaxParticles} | {AudioSources} |");
            return builder.ToString();
        }

        public const string TableHeader =
            "| Escena | Duración | Cámaras | Triángulos (escena) | Renderers | Materiales | Texturas (≈ Android) | Luces puntuales | Partículas máx. | Audio |\n" +
            "|---|---|---|---|---|---|---|---|---|---|";
    }
}

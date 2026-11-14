using System.Collections.Generic;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Mallas procedurales de baja poligonización para los recursos provisionales:
    /// terreno marciano, cúpulas, anillos, icoesferas facetadas (AURA, rocas) y conos de proyección.
    /// </summary>
    internal static class CinematicMeshFactory
    {
        /// <summary>Terreno cuadrado centrado en el origen con relieve fBm, cráteres y una explanada para la base.</summary>
        public static Mesh Terrain(float size, int resolution, int seed, float flatRadius, float height)
        {
            int count = resolution + 1;
            var vertices = new Vector3[count * count];
            var uvs = new Vector2[count * count];
            var random = new System.Random(seed);
            var craters = new List<Vector4>();
            for (int i = 0; i < 7; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = flatRadius * 1.6f + (float)random.NextDouble() * (size * 0.42f - flatRadius * 1.6f);
                float radius = 6f + (float)random.NextDouble() * 18f;
                craters.Add(new Vector4(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, radius, 0f));
            }

            float offset = seed * 0.137f;
            for (int z = 0; z < count; z++)
            {
                for (int x = 0; x < count; x++)
                {
                    float u = x / (float)resolution;
                    float v = z / (float)resolution;
                    float px = (u - 0.5f) * size;
                    float pz = (v - 0.5f) * size;

                    float h = (CinematicBuildKit.Fbm(px * 0.012f + offset, pz * 0.012f + offset, 5) - 0.5f) * height;
                    h += Mathf.Max(0f, CinematicBuildKit.Fbm(px * 0.004f + 13.1f, pz * 0.004f + 7.7f, 3) - 0.45f) * height * 2.2f;

                    foreach (var crater in craters)
                    {
                        float d = Vector2.Distance(new Vector2(px, pz), new Vector2(crater.x, crater.y)) / crater.z;
                        if (d < 1.4f)
                        {
                            float bowl = d < 1f ? -(1f - d * d) * crater.z * 0.22f : 0f;
                            float rim = Mathf.Exp(-Mathf.Pow((d - 1f) * 4f, 2f)) * crater.z * 0.08f;
                            h += bowl + rim;
                        }
                    }

                    float radial = Mathf.Sqrt(px * px + pz * pz);
                    float flatten = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(flatRadius, flatRadius * 1.8f, radial));
                    h *= flatten;

                    vertices[z * count + x] = new Vector3(px, h, pz);
                    uvs[z * count + x] = new Vector2(u, v);
                }
            }

            var triangles = new int[resolution * resolution * 6];
            int t = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = z * count + x;
                    triangles[t++] = i;
                    triangles[t++] = i + count;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + count;
                    triangles[t++] = i + count + 1;
                }
            }

            return Finish(vertices, uvs, triangles);
        }

        /// <summary>Semiesfera unitaria (radio 0,5, base abierta en y = 0), igual escala que las primitivas.</summary>
        public static Mesh Hemisphere(int longitude, int latitude)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int lat = 0; lat <= latitude; lat++)
            {
                float theta = lat / (float)latitude * Mathf.PI * 0.5f;
                for (int lon = 0; lon <= longitude; lon++)
                {
                    float phi = lon / (float)longitude * Mathf.PI * 2f;
                    float ring = Mathf.Cos(theta) * 0.5f;
                    vertices.Add(new Vector3(Mathf.Cos(phi) * ring, Mathf.Sin(theta) * 0.5f, Mathf.Sin(phi) * ring));
                    uvs.Add(new Vector2(lon / (float)longitude, lat / (float)latitude));
                }
            }

            var triangles = new List<int>();
            int stride = longitude + 1;
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    int a = lat * stride + lon;
                    int b = a + stride;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }
            return Finish(vertices.ToArray(), uvs.ToArray(), triangles.ToArray());
        }

        /// <summary>Toro en el plano XZ.</summary>
        public static Mesh Torus(float majorRadius, float minorRadius, int segments, int sides)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var center = new Vector3(Mathf.Cos(a) * majorRadius, 0f, Mathf.Sin(a) * majorRadius);
                var outward = center.normalized;
                for (int k = 0; k <= sides; k++)
                {
                    float b = k / (float)sides * Mathf.PI * 2f;
                    vertices.Add(center + outward * (Mathf.Cos(b) * minorRadius) + Vector3.up * (Mathf.Sin(b) * minorRadius));
                    uvs.Add(new Vector2(s / (float)segments, k / (float)sides));
                }
            }

            var triangles = new List<int>();
            int stride = sides + 1;
            for (int s = 0; s < segments; s++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = s * stride + k;
                    int b = a + stride;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                }
            }
            return Finish(vertices.ToArray(), uvs.ToArray(), triangles.ToArray());
        }

        /// <summary>Cono abierto (sin tapas) a lo largo de +Y; para haces de proyección.</summary>
        public static Mesh Cone(float bottomRadius, float topRadius, float height, int segments)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(direction * bottomRadius);
                vertices.Add(direction * topRadius + Vector3.up * height);
                uvs.Add(new Vector2(s / (float)segments, 0f));
                uvs.Add(new Vector2(s / (float)segments, 1f));
            }

            var triangles = new List<int>();
            for (int s = 0; s < segments; s++)
            {
                int i = s * 2;
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i + 3);
            }
            return Finish(vertices.ToArray(), uvs.ToArray(), triangles.ToArray());
        }

        /// <summary>Icoesfera de radio 0,5. Con desplazamiento sirve para rocas; facetada para AURA.</summary>
        public static Mesh Icosphere(int subdivisions, float noiseAmplitude, int seed, bool flat, Vector3 squash)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++)
                points[i] = points[i].normalized;

            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int level = 0; level < subdivisions; level++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>();
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Midpoint(points, cache, a, b);
                    int bc = Midpoint(points, cache, b, c);
                    int ca = Midpoint(points, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }

            // Orienta todas las caras hacia fuera (en Unity la cara visible cumple cross(b-a, c-a) hacia el observador).
            for (int f = 0; f < faces.Count; f += 3)
            {
                var a = points[faces[f]];
                var b = points[faces[f + 1]];
                var c = points[faces[f + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                {
                    int swap = faces[f + 1];
                    faces[f + 1] = faces[f + 2];
                    faces[f + 2] = swap;
                }
            }

            float offset = seed * 1.731f;
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                float n = 0f;
                if (noiseAmplitude > 0f)
                {
                    n = Mathf.PerlinNoise(p.x * 1.7f + offset, p.y * 1.7f + offset * 0.5f)
                        + Mathf.PerlinNoise(p.z * 1.9f + offset * 0.3f, p.x * 1.9f + 3.1f) - 1f;
                }
                p *= 0.5f * (1f + n * noiseAmplitude);
                points[i] = Vector3.Scale(p, squash);
            }

            if (!flat)
            {
                var uvs = new Vector2[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    var d = points[i].normalized;
                    uvs[i] = new Vector2(0.5f + Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI), 0.5f + Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) / Mathf.PI);
                }
                return Finish(points.ToArray(), uvs, faces.ToArray());
            }

            var flatVertices = new Vector3[faces.Count];
            var flatUvs = new Vector2[faces.Count];
            var flatTriangles = new int[faces.Count];
            for (int i = 0; i < faces.Count; i++)
            {
                flatVertices[i] = points[faces[i]];
                flatUvs[i] = new Vector2((i % 3) * 0.5f, (i % 3) == 2 ? 1f : 0f);
                flatTriangles[i] = i;
            }
            return Finish(flatVertices, flatUvs, flatTriangles);
        }

        static int Midpoint(List<Vector3> points, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (cache.TryGetValue(key, out int index))
                return index;
            points.Add(((points[a] + points[b]) * 0.5f).normalized);
            index = points.Count - 1;
            cache.Add(key, index);
            return index;
        }

        static Mesh Finish(Vector3[] vertices, Vector2[] uvs, int[] triangles)
        {
            var mesh = new Mesh();
            if (vertices.Length > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

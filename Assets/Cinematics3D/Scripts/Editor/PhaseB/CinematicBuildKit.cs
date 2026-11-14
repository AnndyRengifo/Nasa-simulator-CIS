using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>Rutas de los recursos de la Fase B.</summary>
    internal static class PhaseBPaths
    {
        public const string ProceduralModels = CinematicEditorPaths.Models + "/Procedural";
        public const string GeneratedTextures = CinematicEditorPaths.Textures + "/Generated";
        public const string Materials = CinematicEditorPaths.Materials;
        public const string Skies = CinematicEditorPaths.Materials + "/Sky";
        public const string Characters = CinematicEditorPaths.Prefabs + "/Characters";
        public const string CharacterModels = Characters + "/Models";
        public const string Environment = CinematicEditorPaths.Prefabs + "/Environment";
        public const string Props = CinematicEditorPaths.Prefabs + "/Props";
        public const string Fx = CinematicEditorPaths.Prefabs + "/FX";
        public const string AnimAstronaut = CinematicEditorPaths.Animations + "/Astronaut";
        public const string AnimAura = CinematicEditorPaths.Animations + "/AURA";
        public const string AnimProps = CinematicEditorPaths.Animations + "/Props";
        public const string Controllers = CinematicEditorPaths.Animations + "/Controllers";
        public const string GalleryScene = CinematicEditorPaths.Scenes + "/CIN_FaseB_Galeria.unity";
        public const string GalleryTimeline = CinematicEditorPaths.Timelines + "/TL_FaseB_Galeria.playable";
        public const string GalleryAnimations = CinematicEditorPaths.Animations + "/Gallery";

        public static readonly string[] Folders =
        {
            ProceduralModels, GeneratedTextures, Skies, Characters, CharacterModels, Environment, Props, Fx,
            AnimAstronaut, AnimAura, AnimProps, Controllers, GalleryAnimations,
        };

        public static string Prefab(string folder, string name) => $"{folder}/{name}.prefab";
    }

    /// <summary>Estado compartido durante la construcción de la Fase B.</summary>
    internal sealed class BuildContext
    {
        public bool Overwrite;
        public int Layer;
        public CinematicReport Report;
        public int Created;
        public int Updated;
        public int Reused;

        public readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        public readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        public readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        public readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        public readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        public Material Mat(string name)
        {
            if (Materials.TryGetValue(name, out var material))
                return material;
            Report?.Warning("Materiales", $"Material '{name}' no disponible.");
            return null;
        }

        public Mesh MeshAsset(string name) => Meshes.TryGetValue(name, out var mesh) ? mesh : null;
        public AnimationClip Clip(string name) => Clips.TryGetValue(name, out var clip) ? clip : null;
        public GameObject Prefab(string name) => Prefabs.TryGetValue(name, out var prefab) ? prefab : null;

        public void Count(bool existed, bool written)
        {
            if (!existed) Created++;
            else if (written) Updated++;
            else Reused++;
        }
    }

    /// <summary>Utilidades para crear assets (mallas, texturas, materiales, clips, prefabs) de forma idempotente.</summary>
    internal static class CinematicBuildKit
    {
        // ------------------------------------------------------------------ Mallas

        public static Mesh SaveMesh(BuildContext ctx, Mesh mesh, string name)
        {
            string path = $"{PhaseBPaths.ProceduralModels}/{name}.asset";
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                ctx.Count(false, true);
                ctx.Meshes[name] = mesh;
                return mesh;
            }

            if (ctx.Overwrite)
            {
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices;
                existing.normals = mesh.normals;
                existing.tangents = mesh.tangents;
                existing.uv = mesh.uv;
                existing.triangles = mesh.triangles;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
            }
            ctx.Count(true, ctx.Overwrite);
            Object.DestroyImmediate(mesh);
            ctx.Meshes[name] = existing;
            return existing;
        }

        // ------------------------------------------------------------------ Texturas

        internal struct TextureSpec
        {
            public TextureWrapMode Wrap;
            public int MaxSize;
            public bool Mipmaps;
            public bool Alpha;
        }

        public static Texture2D GetTexture(BuildContext ctx, string name, int width, int height, Func<Color32[]> generate, TextureSpec spec)
        {
            string path = $"{PhaseBPaths.GeneratedTextures}/{name}.png";
            bool existed = File.Exists(path);
            if (existed && !ctx.Overwrite)
            {
                var cached = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (cached != null)
                {
                    ctx.Count(true, false);
                    ctx.Textures[name] = cached;
                    return cached;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(generate());
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.wrapMode = spec.Wrap;
                importer.maxTextureSize = spec.MaxSize > 0 ? spec.MaxSize : 1024;
                importer.mipmapEnabled = spec.Mipmaps;
                importer.alphaSource = spec.Alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = spec.Alpha;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            ctx.Count(existed, true);
            ctx.Textures[name] = result;
            return result;
        }

        // ------------------------------------------------------------------ Materiales

        public static Material GetMaterial(BuildContext ctx, string folder, string name, string shaderName, Action<Material> setup)
        {
            string path = $"{folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && !ctx.Overwrite)
            {
                ctx.Count(true, false);
                ctx.Materials[name] = existing;
                return existing;
            }

            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                ctx.Report.Error("Materiales", $"No se encontró el shader '{shaderName}' para {name}.");
                return existing;
            }

            var material = new Material(shader) { name = name, enableInstancing = true };
            setup?.Invoke(material);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
                ctx.Count(false, true);
                ctx.Materials[name] = material;
                return material;
            }

            existing.shader = shader;
            EditorUtility.CopySerialized(material, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(material);
            ctx.Count(true, true);
            ctx.Materials[name] = existing;
            return existing;
        }

        /// <summary>Configura un material URP (Lit/Unlit/Particles) como transparente (alfa o aditivo).</summary>
        public static void MakeTransparent(Material material, bool additive, bool doubleSided)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", doubleSided ? 0f : 2f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
        }

        public static void SetEmission(Material material, Color color, float intensity)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        // ------------------------------------------------------------------ Clips y controladores

        public static AnimationClip SaveClip(BuildContext ctx, AnimationClip clip, string folder)
        {
            string path = $"{folder}/{clip.name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(clip, path);
                ctx.Count(false, true);
                ctx.Clips[clip.name] = clip;
                return clip;
            }

            if (ctx.Overwrite)
            {
                string name = existing.name;
                EditorUtility.CopySerialized(clip, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
            }
            ctx.Count(true, ctx.Overwrite);
            Object.DestroyImmediate(clip);
            ctx.Clips[existing.name] = existing;
            return existing;
        }

        /// <summary>Controlador con un único estado en bucle (las cinemáticas lo sustituyen mientras Timeline anima).</summary>
        public static AnimatorController GetLoopController(BuildContext ctx, string name, AnimationClip clip)
        {
            string path = $"{PhaseBPaths.Controllers}/{name}.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null)
            {
                ctx.Count(true, false);
                return existing;
            }
            if (clip == null)
                return null;
            var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
            ctx.Count(false, true);
            return controller;
        }

        public static void SetupAnimator(GameObject go, RuntimeAnimatorController controller)
        {
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
        }

        // ------------------------------------------------------------------ Prefabs

        public static GameObject GetPrefab(BuildContext ctx, string folder, string name, Func<GameObject> build)
        {
            string path = PhaseBPaths.Prefab(folder, name);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !ctx.Overwrite)
            {
                ctx.Count(true, false);
                ctx.Prefabs[name] = existing;
                return existing;
            }

            GameObject instance = null;
            try
            {
                instance = build();
                instance.name = name;
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool success);
                if (!success || prefab == null)
                {
                    ctx.Report.Error("Prefabs", $"No se pudo guardar {path}.");
                    return existing;
                }
                ctx.Count(existing != null, true);
                ctx.Prefabs[name] = prefab;
                return prefab;
            }
            catch (Exception exception)
            {
                ctx.Report.Error("Prefabs", $"Error al construir {name}: {exception.Message}");
                Debug.LogException(exception);
                return existing;
            }
            finally
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
            }
        }

        public static GameObject Nest(BuildContext ctx, string prefabName, Transform parent, Vector3 position, Vector3 euler, string instanceName = null)
        {
            var prefab = ctx.Prefab(prefabName);
            if (prefab == null)
            {
                ctx.Report.Warning("Prefabs", $"No se pudo anidar '{prefabName}' (no existe).");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localEulerAngles = euler;
            if (!string.IsNullOrEmpty(instanceName))
                instance.name = instanceName;
            return instance;
        }

        // ------------------------------------------------------------------ Objetos de escena

        public static GameObject Obj(string name, Transform parent, int layer, Vector3 position = default, Vector3 euler = default)
        {
            var go = new GameObject(name) { layer = layer };
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            return go;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Material material, int layer,
            Vector3 position, Vector3 euler, Vector3 scale, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = layer;
            if (go.TryGetComponent<Collider>(out var collider))
                Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null)
                renderer.sharedMaterial = material;
            if (!castShadows)
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material material, int layer,
            Vector3 position, Vector3 euler, Vector3 scale, bool castShadows = true)
        {
            var go = Obj(name, parent, layer, position, euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (!castShadows)
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static CinematicBindingTarget AddBindingTarget(GameObject go, string key, Transform visualRoot)
        {
            var target = go.GetComponent<CinematicBindingTarget>();
            if (target == null)
                target = go.AddComponent<CinematicBindingTarget>();
            var serialized = new SerializedObject(target);
            serialized.FindProperty("bindingKey").stringValue = key;
            serialized.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }

        public static Light AddPointLight(Transform parent, string name, int layer, Vector3 position, Color color, float intensity, float range)
        {
            var go = Obj(name, parent, layer, position);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.cullingMask = layer >= 0 ? 1 << layer : ~0;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        public static Color Hex(string html) => CinematicProjectSetup.Hex(html);

        public static float Fbm(float x, float y, int octaves)
        {
            float value = 0f, amplitude = 0.5f, frequency = 1f;
            for (int i = 0; i < octaves; i++)
            {
                value += amplitude * Mathf.PerlinNoise(x * frequency, y * frequency);
                frequency *= 2.03f;
                amplitude *= 0.5f;
            }
            return value;
        }
    }

    /// <summary>
    /// Construye clips genéricos a partir de poses clave (rotaciones locales en grados).
    /// Los huesos que no aparecen en una pose toman la pose de reposo.
    /// </summary>
    internal sealed class PoseClipBuilder
    {
        readonly string m_Name;
        readonly float m_FrameRate;
        readonly bool m_Loop;
        readonly Dictionary<string, Vector3> m_Rest;
        readonly List<(float time, Dictionary<string, Vector3> pose)> m_Keys = new List<(float, Dictionary<string, Vector3>)>();
        readonly List<(string path, Type type, string property, AnimationCurve curve)> m_Extra = new List<(string, Type, string, AnimationCurve)>();

        public PoseClipBuilder(string name, bool loop, Dictionary<string, Vector3> restPose, float frameRate = 30f)
        {
            m_Name = name;
            m_Loop = loop;
            m_Rest = restPose;
            m_FrameRate = frameRate;
        }

        public PoseClipBuilder Key(float time, Dictionary<string, Vector3> pose)
        {
            m_Keys.Add((time, pose ?? new Dictionary<string, Vector3>()));
            return this;
        }

        public PoseClipBuilder Curve(string path, Type type, string property, AnimationCurve curve)
        {
            m_Extra.Add((path, type, property, curve));
            return this;
        }

        /// <summary>Anima solo la altura local de un objeto (X y Z quedan fijas en el valor indicado).</summary>
        public PoseClipBuilder Height(string path, AnimationCurve y, float x = 0f, float z = 0f)
        {
            float length = y.length > 0 ? y[y.length - 1].time : 1f;
            Curve(path, typeof(Transform), "m_LocalPosition.x", Constant(length, x));
            Curve(path, typeof(Transform), "m_LocalPosition.y", y);
            Curve(path, typeof(Transform), "m_LocalPosition.z", Constant(length, z));
            return this;
        }

        public AnimationClip Build()
        {
            var clip = new AnimationClip { name = m_Name, frameRate = m_FrameRate };
            var bones = new HashSet<string>();
            foreach (var key in m_Keys)
                foreach (var bone in key.pose.Keys)
                    bones.Add(bone);

            foreach (var bone in bones)
            {
                var x = new AnimationCurve();
                var y = new AnimationCurve();
                var z = new AnimationCurve();
                foreach (var key in m_Keys)
                {
                    var value = key.pose.TryGetValue(bone, out var v) ? v : (m_Rest.TryGetValue(bone, out var r) ? r : Vector3.zero);
                    x.AddKey(key.time, value.x);
                    y.AddKey(key.time, value.y);
                    z.AddKey(key.time, value.z);
                }
                Smooth(x);
                Smooth(y);
                Smooth(z);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bone, typeof(Transform), "localEulerAnglesRaw.x"), x);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bone, typeof(Transform), "localEulerAnglesRaw.y"), y);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bone, typeof(Transform), "localEulerAnglesRaw.z"), z);
            }

            foreach (var extra in m_Extra)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(extra.path, extra.type, extra.property), extra.curve);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = m_Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        public static void Smooth(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
        }

        public static AnimationCurve Keys(float[] times, float[] values)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < times.Length; i++)
                curve.AddKey(times[i], values[i]);
            Smooth(curve);
            return curve;
        }

        public static AnimationCurve Linear(float duration, float from, float to) => AnimationCurve.Linear(0f, from, duration, to);

        public static AnimationCurve Constant(float duration, float value) => AnimationCurve.Constant(0f, duration, value);
    }
}

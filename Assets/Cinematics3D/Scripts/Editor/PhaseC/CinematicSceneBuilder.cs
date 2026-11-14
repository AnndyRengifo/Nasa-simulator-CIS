#if SOL3_CINEMACHINE && SOL3_SPLINES
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.Timeline;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Construye una escena cinemática completa siguiendo el estándar (CIN_Template):
    /// raíz con CinematicSceneRoot + PlayableDirector, cámara con CinemachineBrain y renderer 3D,
    /// sol animable, volumen de post-procesado, Timeline TL_&lt;id&gt;, subtítulos SUB_&lt;id&gt;_es
    /// y registro de la escena en el catálogo (definición) y en la build.
    /// Todas las pistas se nombran con su clave de enlace (sustitución de Nivel 3).
    /// </summary>
    internal sealed class CinematicSceneBuilder
    {
        public readonly BuildContext Ctx;
        public readonly string Id;
        public readonly Transform Cameras, Lighting, Environment, Actors, Fx;
        public PlayableDirector Director { get; private set; }
        public TimelineAsset Timeline { get; private set; }
        public Light Sun { get; private set; }

        readonly int m_Layer;
        readonly Scene m_Scene;
        readonly GameObject m_Root;
        readonly Camera m_Camera;
        readonly CinemachineBrain m_Brain;
        readonly GameObject m_SunRig;
        readonly CinemachineTrack m_BrainTrack;
        readonly GroupTrack m_CameraGroup, m_ActorGroup, m_SetGroup, m_LightGroup;
        readonly List<(TrackAsset track, Object target)> m_Bindings = new List<(TrackAsset, Object)>();
        readonly List<(string speaker, string text, float start, float end)> m_Lines = new List<(string, string, float, float)>();
        readonly NoiseSettings m_Noise;

        string AnimationFolder => $"{CinematicEditorPaths.Animations}/{Id}";

        public CinematicSceneBuilder(BuildContext ctx, string id, int rendererIndex)
        {
            Ctx = ctx;
            Id = id;
            m_Layer = ctx.Layer;
            int mask = 1 << m_Layer;
            CinematicProjectSetup.EnsureFolder(AnimationFolder);

            // Escena primero: abrir una escena descarga los assets recién creados que no estén referenciados.
            m_Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CinematicPhaseC.LoadExisting(ctx);

            m_Root = CinematicBuildKit.Obj("[CINEMATIC_ROOT]", null, m_Layer);
            Director = m_Root.AddComponent<PlayableDirector>();
            m_Root.AddComponent<CinematicSceneRoot>();
            Cameras = CinematicBuildKit.Obj("Cameras", null, m_Layer).transform;
            Lighting = CinematicBuildKit.Obj("Lighting", null, m_Layer).transform;
            Environment = CinematicBuildKit.Obj("Environment", null, m_Layer).transform;
            Actors = CinematicBuildKit.Obj("Actors", null, m_Layer).transform;
            Fx = CinematicBuildKit.Obj("FX", null, m_Layer).transform;

            // Sol: el Animator está en el rig y la luz en un hijo (evita desplazamientos de raíz en Timeline).
            m_SunRig = CinematicBuildKit.Obj("SunRig", Lighting, m_Layer);
            m_SunRig.AddComponent<Animator>();
            CinematicBuildKit.AddBindingTarget(m_SunRig, "SunRig", null);
            var sunObject = CinematicBuildKit.Obj("Sun", m_SunRig.transform, m_Layer, Vector3.zero, new Vector3(30f, -40f, 0f));
            Sun = sunObject.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = CinematicBuildKit.Hex("#FFD9B3");
            Sun.intensity = 1.5f;
            Sun.shadows = LightShadows.Hard;
            Sun.cullingMask = mask;
            Sun.lightmapBakeType = LightmapBakeType.Realtime;

            var volume = CinematicBuildKit.Obj("GlobalVolume", Lighting, m_Layer).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CinematicEditorPaths.DefaultVolumeProfile);

            var cameraObject = CinematicBuildKit.Obj("CinematicCamera", Cameras, m_Layer);
            m_Camera = cameraObject.AddComponent<Camera>();
            m_Camera.clearFlags = CameraClearFlags.Skybox;
            m_Camera.cullingMask = mask;
            m_Camera.fieldOfView = 45f;
            m_Camera.nearClipPlane = 0.1f;
            m_Camera.farClipPlane = 1500f;
            m_Camera.allowMSAA = false;
            var data = m_Camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(rendererIndex);
            data.renderPostProcessing = true;
            data.volumeLayerMask = mask;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            data.renderShadows = true;
            cameraObject.AddComponent<AudioListener>();
            m_Brain = cameraObject.AddComponent<CinemachineBrain>();
            m_Brain.IgnoreTimeScale = true;
            m_Brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            m_Brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            m_Brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1f);
            CinematicBuildKit.AddBindingTarget(cameraObject, CinematicBindingKeys.Brain, null);

            m_Noise = AssetDatabase.LoadAssetAtPath<NoiseSettings>(CinematicEditorPaths.NoiseMild);

            string timelinePath = CinematicEditorPaths.TimelineFor(id);
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath) != null)
                AssetDatabase.DeleteAsset(timelinePath);
            Timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(Timeline, timelinePath);

            m_CameraGroup = Timeline.CreateTrack<GroupTrack>(null, "CÁMARAS");
            m_BrainTrack = Timeline.CreateTrack<CinemachineTrack>(m_CameraGroup, CinematicBindingKeys.Brain);
            m_ActorGroup = Timeline.CreateTrack<GroupTrack>(null, "ACTORES");
            m_SetGroup = Timeline.CreateTrack<GroupTrack>(null, "ESCENARIO");
            m_LightGroup = Timeline.CreateTrack<GroupTrack>(null, "ILUMINACIÓN Y AMBIENTE");
            m_Bindings.Add((m_BrainTrack, m_Brain));
        }

        // ------------------------------------------------------------------ Objetos

        public GameObject Place(string prefab, Transform parent, Vector3 position, Vector3 euler, string name = null) =>
            CinematicBuildKit.Nest(Ctx, prefab, parent, position, euler, name);

        public Transform Look(string name, Vector3 position) =>
            CinematicBuildKit.Obj(name, Environment, m_Layer, position).transform;

        /// <summary>Cambia la clave de enlace de una instancia (cuando hay varias del mismo actor).</summary>
        public void Key(GameObject target, string key)
        {
            var existing = target.GetComponent<CinematicBindingTarget>();
            var visual = existing != null ? existing.VisualRoot : null;
            CinematicBuildKit.AddBindingTarget(target, key, visual);
        }

        // ------------------------------------------------------------------ Cámaras

        public CinemachineCamera Cam(string name, Vector3 position, Transform look, float fov, float noise = 0.3f)
        {
            var go = CinematicBuildKit.Obj($"CM_{Id}_{name}", Cameras, m_Layer, position);
            var camera = go.AddComponent<CinemachineCamera>();
            camera.Lens.FieldOfView = fov;
            camera.Lens.NearClipPlane = 0.1f;
            camera.Lens.FarClipPlane = 1500f;
            camera.Target.TrackingTarget = look;
            go.AddComponent<CinemachineRotationComposer>();
            if (noise > 0f && m_Noise != null)
            {
                var perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
                perlin.NoiseProfile = m_Noise;
                perlin.AmplitudeGain = noise;
                perlin.FrequencyGain = 0.6f;
            }
            if (look != null && look.position != position)
                go.transform.rotation = Quaternion.LookRotation(look.position - position);
            return camera;
        }

        /// <summary>Garantiza que el objeto tenga como clave de enlace el nombre de su pista (convención de Nivel 3).</summary>
        void EnsureKey(GameObject target, string key)
        {
            var existing = target.GetComponent<CinematicBindingTarget>();
            if (existing == null || existing.BindingKey != key)
                Key(target, key);
        }

        /// <summary>Cámara sobre un recorrido (spline) animado por el Timeline entre start y start+duration.</summary>
        public CinemachineCamera Dolly(string name, Vector3[] knots, Transform look, float fov, double start, double duration, float noise = 0f)
        {
            var path = CinematicBuildKit.Obj($"Dolly_{Id}_{name}", Cameras, m_Layer);
            var container = path.AddComponent<SplineContainer>();
            var spline = container.Spline;
            if (spline == null)
            {
                spline = new Spline();
                container.Spline = spline;
            }
            spline.Clear();
            foreach (var knot in knots)
                spline.Add(new Unity.Mathematics.float3(knot.x, knot.y, knot.z), TangentMode.AutoSmooth);

            var camera = Cam(name, knots[0], look, fov, noise);
            var dolly = camera.gameObject.AddComponent<CinemachineSplineDolly>();
            dolly.Spline = container;
            dolly.PositionUnits = PathIndexUnit.Normalized;
            dolly.CameraPosition = 0f;
            var animator = camera.gameObject.AddComponent<Animator>();
            CinematicBuildKit.AddBindingTarget(camera.gameObject, camera.name, null);

            var clip = new AnimationClip { name = $"AC_{Id}_Dolly_{name}", frameRate = 30f };
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(CinemachineSplineDolly), "m_SplineSettings.Position"),
                AnimationCurve.EaseInOut(0f, 0f, (float)duration, 1f));
            clip = CinematicBuildKit.SaveClip(new BuildContext { Overwrite = true, Report = Ctx.Report }, clip, AnimationFolder);

            var track = Timeline.CreateTrack<AnimationTrack>(m_CameraGroup, camera.name);
            var timelineClip = track.CreateClip(clip);
            timelineClip.start = start;
            timelineClip.duration = duration;
            timelineClip.displayName = "Dolly";
            m_Bindings.Add((track, animator));
            return camera;
        }

        /// <summary>Plano: la cámara está activa entre start y start+duration. Solapar dos planos = fundido de cámara.</summary>
        public void Shot(CinemachineCamera camera, double start, double duration)
        {
            var clip = m_BrainTrack.CreateClip<CinemachineShot>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = camera.name;
            var shot = (CinemachineShot)clip.asset;
            shot.DisplayName = camera.name;
            shot.VirtualCamera.exposedName = new PropertyName(System.Guid.NewGuid().ToString("N"));
            Director.SetReferenceValue(shot.VirtualCamera.exposedName, camera);
            EditorUtility.SetDirty(shot);
        }

        // ------------------------------------------------------------------ Pistas

        /// <summary>Pista de animación para un actor o prop. El nombre de la pista es su clave de enlace.</summary>
        public AnimationTrack Track(GameObject target, string key, bool props = false)
        {
            if (target == null)
                return null;
            EnsureKey(target, key);
            var animator = target.GetComponent<Animator>();
            if (animator == null)
                animator = target.AddComponent<Animator>();
            var track = Timeline.CreateTrack<AnimationTrack>(props ? m_SetGroup : m_ActorGroup, key);
            m_Bindings.Add((track, animator));
            return track;
        }

        /// <summary>Añade clips en secuencia (fundido de 0,3 s entre ellos). Devuelve el final.</summary>
        public double Clips(AnimationTrack track, double start, params (string clip, double duration)[] clips)
        {
            double time = start;
            if (track == null)
                return time;
            foreach (var entry in clips)
            {
                var clip = Ctx.Clip(entry.clip);
                if (clip == null)
                {
                    Ctx.Report.Warning(Id, $"Falta la animación {entry.clip}.");
                    continue;
                }
                var timelineClip = track.CreateClip(clip);
                timelineClip.start = time;
                timelineClip.duration = entry.duration;
                timelineClip.displayName = clip.name.Replace("AC_", string.Empty);
                time += entry.duration - 0.3;
            }
            return time + 0.3;
        }

        /// <summary>Pista de activación: el objeto solo está activo durante los intervalos indicados.</summary>
        public void Active(GameObject target, string key, params (double start, double duration)[] ranges)
        {
            if (target == null)
                return;
            var track = Timeline.CreateTrack<ActivationTrack>(m_LightGroup, key);
            track.postPlaybackState = ActivationTrack.PostPlaybackState.LeaveAsIs;
            EnsureKey(target, key);
            foreach (var range in ranges)
            {
                var clip = track.CreateDefaultClip();
                clip.start = range.start;
                clip.duration = range.duration;
                clip.displayName = "Activo";
            }
            m_Bindings.Add((track, target));
        }

        /// <summary>
        /// Anima el sol (inclinación, intensidad y color) entre fotogramas clave y mantiene el último
        /// estado hasta <paramref name="holdUntil"/>.
        /// </summary>
        public void SunMotion(double start, double holdUntil, (float time, float pitch, float intensity, Color color)[] keys)
        {
            const string path = "Sun";
            float yaw = Sun.transform.localEulerAngles.y;
            var times = new float[keys.Length];
            var pitch = new float[keys.Length];
            var intensity = new float[keys.Length];
            var r = new float[keys.Length];
            var g = new float[keys.Length];
            var b = new float[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                times[i] = keys[i].time;
                pitch[i] = keys[i].pitch;
                intensity[i] = keys[i].intensity;
                r[i] = keys[i].color.r;
                g[i] = keys[i].color.g;
                b[i] = keys[i].color.b;
            }
            float length = times[times.Length - 1];

            var builder = new PoseClipBuilder($"AC_{Id}_SunMotion", false, new Dictionary<string, Vector3>());
            builder.Curve(path, typeof(Transform), "localEulerAnglesRaw.x", PoseClipBuilder.Keys(times, pitch));
            builder.Curve(path, typeof(Transform), "localEulerAnglesRaw.y", PoseClipBuilder.Constant(length, yaw));
            builder.Curve(path, typeof(Transform), "localEulerAnglesRaw.z", PoseClipBuilder.Constant(length, 0f));
            builder.Curve(path, typeof(Light), "m_Intensity", PoseClipBuilder.Keys(times, intensity));
            builder.Curve(path, typeof(Light), "m_Color.r", PoseClipBuilder.Keys(times, r));
            builder.Curve(path, typeof(Light), "m_Color.g", PoseClipBuilder.Keys(times, g));
            builder.Curve(path, typeof(Light), "m_Color.b", PoseClipBuilder.Keys(times, b));
            var clip = CinematicBuildKit.SaveClip(new BuildContext { Overwrite = true, Report = Ctx.Report }, builder.Build(), AnimationFolder);

            var track = Timeline.CreateTrack<AnimationTrack>(m_LightGroup, "SunRig");
            var timelineClip = track.CreateClip(clip);
            timelineClip.start = start;
            timelineClip.duration = System.Math.Max(length, holdUntil - start);
            timelineClip.displayName = "Sol";
            m_Bindings.Add((track, m_SunRig.GetComponent<Animator>()));

            Sun.transform.localEulerAngles = new Vector3(keys[0].pitch, yaw, 0f);
            Sun.intensity = keys[0].intensity;
            Sun.color = keys[0].color;
        }

        /// <summary>Ambiente (cielo, niebla, luz ambiente) que se activa en los tramos indicados.</summary>
        public GameObject Ambience(string name, string sky, bool fog, Color fogColor, float density, Color skyAmbient, Color equator, Color ground)
        {
            var go = CinematicBuildKit.Obj(name, Lighting, m_Layer);
            var profile = go.AddComponent<CinematicEnvironmentProfile>();
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("skybox").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>($"{PhaseBPaths.Skies}/{sky}.mat");
            serialized.FindProperty("fog").boolValue = fog;
            serialized.FindProperty("fogColor").colorValue = fogColor;
            serialized.FindProperty("fogDensity").floatValue = density;
            serialized.FindProperty("ambientSky").colorValue = skyAmbient;
            serialized.FindProperty("ambientEquator").colorValue = equator;
            serialized.FindProperty("ambientGround").colorValue = ground;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);
            return go;
        }

        // ------------------------------------------------------------------ Diálogo

        public void Line(string speaker, string text, float start, float end) => m_Lines.Add((speaker, text, start, end));

        // ------------------------------------------------------------------ Final

        /// <summary>Guarda escena, Timeline y subtítulos, y registra todo en la definición del catálogo.</summary>
        public void Finish(string baseSky, Color fogColor, float fogDensity, Color skyAmbient, Color equator, Color ground)
        {
            string category = $"Cinemática '{Id}'";
            EditorUtility.SetDirty(Timeline);
            AssetDatabase.SaveAssets();

            Director.playableAsset = Timeline;
            Director.playOnAwake = false;
            Director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            Director.extrapolationMode = DirectorWrapMode.Hold;
            foreach (var binding in m_Bindings)
            {
                if (binding.target != null)
                    Director.SetGenericBinding(binding.track, binding.target);
            }

            var sheet = SaveSubtitles();

            var serializedRoot = new SerializedObject(m_Root.GetComponent<CinematicSceneRoot>());
            serializedRoot.FindProperty("cinematicId").stringValue = Id;
            serializedRoot.FindProperty("director").objectReferenceValue = Director;
            serializedRoot.FindProperty("cinematicCamera").objectReferenceValue = m_Camera;
            serializedRoot.FindProperty("previewSubtitles").objectReferenceValue = sheet;
            serializedRoot.ApplyModifiedPropertiesWithoutUndo();

            var sky = AssetDatabase.LoadAssetAtPath<Material>($"{PhaseBPaths.Skies}/{baseSky}.mat");
            if (sky != null)
                RenderSettings.skybox = sky;
            RenderSettings.sun = Sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = skyAmbient;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = fogDensity > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            var lighting = AssetDatabase.LoadAssetAtPath<LightingSettings>(CinematicEditorPaths.LightingSettings);
            if (lighting != null)
                Lightmapping.lightingSettings = lighting;

            string scenePath = CinematicEditorPaths.SceneFor(Id);
            EditorSceneManager.MarkSceneDirty(m_Scene);
            if (!EditorSceneManager.SaveScene(m_Scene, scenePath))
            {
                Ctx.Report.Error(category, $"No se pudo guardar {scenePath}.");
                return;
            }

            SceneReferenceDrawer.AddToBuild(scenePath);
            RegisterInCatalog(scenePath, sheet);
            Ctx.Report.Ok(category, $"{Path.GetFileName(scenePath)} + TL_{Id} ({Timeline.duration:0.0} s, {m_BrainTrack.GetClips().Count()} planos, " +
                                    $"{m_Lines.Count} líneas de diálogo). Registrada en la build y en CinematicDef_{Id}.");
        }

        SubtitleSheet SaveSubtitles()
        {
            string path = CinematicEditorPaths.SubtitlesFor(Id, "es");
            var sheet = AssetDatabase.LoadAssetAtPath<SubtitleSheet>(path);
            if (sheet == null)
            {
                sheet = ScriptableObject.CreateInstance<SubtitleSheet>();
                AssetDatabase.CreateAsset(sheet, path);
            }

            var speakers = new Dictionary<string, CinematicSpeaker>
            {
                { "AURA", AssetDatabase.LoadAssetAtPath<CinematicSpeaker>($"{CinematicEditorPaths.Speakers}/SPK_AURA.asset") },
                { "CT", AssetDatabase.LoadAssetAtPath<CinematicSpeaker>($"{CinematicEditorPaths.Speakers}/SPK_ControlTierra.asset") },
            };

            var serialized = new SerializedObject(sheet);
            serialized.FindProperty("cinematicId").stringValue = Id;
            serialized.FindProperty("language").stringValue = "es";
            var lines = serialized.FindProperty("lines");
            lines.arraySize = m_Lines.Count;
            for (int i = 0; i < m_Lines.Count; i++)
            {
                var element = lines.GetArrayElementAtIndex(i);
                var line = m_Lines[i];
                element.FindPropertyRelative("lineId").stringValue = $"{Id}_{(i + 1) * 10:000}";
                element.FindPropertyRelative("speaker").objectReferenceValue = speakers.TryGetValue(line.speaker, out var speaker) ? speaker : null;
                element.FindPropertyRelative("text").stringValue = line.text;
                element.FindPropertyRelative("startTime").floatValue = line.start;
                element.FindPropertyRelative("endTime").floatValue = line.end;
                element.FindPropertyRelative("voiceClip").objectReferenceValue = null;
                element.FindPropertyRelative("voiceVolume").floatValue = 1f;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sheet);
            AssetDatabase.SaveAssets();
            return sheet;
        }

        void RegisterInCatalog(string scenePath, SubtitleSheet sheet)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CinematicDefinition>(CinematicEditorPaths.DefinitionFor(Id));
            if (definition == null)
            {
                Ctx.Report.Warning($"Cinemática '{Id}'", $"No existe CinematicDef_{Id}: ejecuta la configuración de la Fase A.");
                return;
            }

            var serialized = new SerializedObject(definition);
            var scene = serialized.FindProperty("scene");
            scene.FindPropertyRelative("path").stringValue = scenePath;
            scene.FindPropertyRelative("guid").stringValue = AssetDatabase.AssetPathToGUID(scenePath);
            serialized.FindProperty("expectedDuration").floatValue = Mathf.Ceil((float)Timeline.duration);
            var sheets = serialized.FindProperty("subtitleSheets");
            bool found = false;
            for (int i = 0; i < sheets.arraySize; i++)
                found |= sheets.GetArrayElementAtIndex(i).objectReferenceValue == sheet;
            if (!found)
            {
                sheets.arraySize++;
                sheets.GetArrayElementAtIndex(sheets.arraySize - 1).objectReferenceValue = sheet;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif

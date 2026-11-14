using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
#if SOL3_CINEMACHINE && SOL3_SPLINES
using Unity.Cinemachine;
using UnityEngine.Splines;
#endif

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Escena de prueba de la Fase B (CIN_FaseB_Galeria): recorre todos los personajes, escenarios,
    /// props y efectos con cámaras Cinemachine y un Timeline, usando las mismas convenciones que
    /// usarán las cinemáticas reales. No forma parte de la build del juego.
    /// </summary>
    internal static class CinematicGalleryBuilder
    {
        public const string GalleryId = "gallery_fase_b";

        static readonly Vector3 ExteriorStation = Vector3.zero;
        static readonly Vector3 AstronautStation = new Vector3(2000f, 0f, 0f);
        static readonly Vector3 AuraStation = new Vector3(4000f, 0f, 0f);
        static readonly Vector3 ControlStation = new Vector3(6000f, 0f, 0f);
        static readonly Vector3 PowerStation = new Vector3(8000f, 0f, 0f);
        static readonly Vector3 ShelterStation = new Vector3(10000f, 0f, 0f);
        static readonly Vector3 EarthStation = new Vector3(12000f, 0f, 0f);
        static readonly Vector3 SpaceStation = new Vector3(14000f, 0f, 0f);

        public static bool Build(BuildContext ctx, int rendererIndex)
        {
            const string category = "Galería";
#if !(SOL3_CINEMACHINE && SOL3_SPLINES)
            ctx.Report.Error(category, "Cinemachine o Splines no están instalados.");
            return false;
#else
            int layer = ctx.Layer;
            int mask = 1 << layer;
            var scene = EditorSceneManager.GetActiveScene();

            var root = CinematicBuildKit.Obj("[CINEMATIC_ROOT]", null, layer);
            var director = root.AddComponent<PlayableDirector>();
            var sceneRoot = root.AddComponent<CinematicSceneRoot>();
            var cameras = CinematicBuildKit.Obj("Cameras", null, layer).transform;
            var lighting = CinematicBuildKit.Obj("Lighting", null, layer).transform;
            var environment = CinematicBuildKit.Obj("Environment", null, layer).transform;
            var actors = CinematicBuildKit.Obj("Actors", null, layer).transform;
            var fx = CinematicBuildKit.Obj("FX", null, layer).transform;

            // ---------------------------------------------------------------- Iluminación global
            var sunObject = CinematicBuildKit.Obj("Sun", lighting, layer, Vector3.zero, new Vector3(32f, -40f, 0f));
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = CinematicBuildKit.Hex("#FFD9B3");
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Hard;
            sun.cullingMask = mask;
            sun.lightmapBakeType = LightmapBakeType.Realtime;

            var volumeObject = CinematicBuildKit.Obj("GlobalVolume", lighting, layer);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CinematicEditorPaths.DefaultVolumeProfile);

            // ---------------------------------------------------------------- Escenarios
            Place(ctx, "PF_Set_MarsSurface", environment, ExteriorStation, Vector3.zero);
            Place(ctx, "PF_FX_MarsDust", fx, ExteriorStation + new Vector3(0f, 3f, 0f), Vector3.zero);

            Platform(ctx, environment, "Platform_Astronaut", AstronautStation);
            Platform(ctx, environment, "Platform_AURA", AuraStation);
            Place(ctx, "PF_Prop_HoloTable", environment, AuraStation + new Vector3(0f, 0.1f, 0f), Vector3.zero);

            var control = Place(ctx, "PF_Room_ControlCenter", environment, ControlStation, Vector3.zero);
            var power = Place(ctx, "PF_Room_PowerRoom", environment, PowerStation, Vector3.zero);
            Place(ctx, "PF_FX_Sparks", fx, PowerStation + new Vector3(2.1f, 2.6f, -1.5f), new Vector3(-30f, -60f, 0f));
            Place(ctx, "PF_Room_Shelter", environment, ShelterStation, Vector3.zero);
            Place(ctx, "PF_Room_EarthControl", environment, EarthStation, Vector3.zero);

            Place(ctx, "PF_Space_Mars", environment, SpaceStation, Vector3.zero);
            Place(ctx, "PF_Space_Earth", environment, SpaceStation + new Vector3(120f, 45f, 160f), Vector3.zero);
            var sunBody = SpaceStation + new Vector3(-260f, 80f, 190f);
            Place(ctx, "PF_Space_Sun", environment, sunBody, Vector3.zero);

            // ---------------------------------------------------------------- Actores
            var astronaut = Place(ctx, "PF_Astronaut", actors, AstronautStation + new Vector3(0f, 0.1f, 0f), new Vector3(0f, 200f, 0f));
            var aura = Place(ctx, "PF_AURA", actors, AuraStation + new Vector3(0f, 1.85f, 0f), Vector3.zero);
            var auraInRoom = Place(ctx, "PF_AURA", actors, ControlStation + new Vector3(0f, 1.75f, -1.2f), Vector3.zero);
            if (auraInRoom != null)
                auraInRoom.name = "PF_AURA_ControlRoom";
            Place(ctx, "PF_Astronaut", actors, ControlStation + new Vector3(0f, 0f, 1.9f), Vector3.zero);
            Place(ctx, "PF_Astronaut", actors, PowerStation + new Vector3(0f, 0f, -1.4f), Vector3.zero);
            Place(ctx, "PF_Astronaut", actors, ShelterStation, new Vector3(0f, 150f, 0f));
            var transmission = Place(ctx, "PF_FX_TransmissionPulse", fx, ExteriorStation + new Vector3(8f, 10.5f, 15.8f), Vector3.zero);

            // ---------------------------------------------------------------- Cámara física
            var cameraObject = CinematicBuildKit.Obj("CinematicCamera", cameras, layer, AstronautStation + new Vector3(2f, 1.6f, -4f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = mask;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1500f;
            camera.allowMSAA = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(rendererIndex);
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = mask;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.renderShadows = true;
            cameraObject.AddComponent<AudioListener>();
            var brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.IgnoreTimeScale = true;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.5f);
            CinematicBuildKit.AddBindingTarget(cameraObject, CinematicBindingKeys.Brain, null);

            var noise = AssetDatabase.LoadAssetAtPath<NoiseSettings>(CinematicEditorPaths.NoiseMild);

            // ---------------------------------------------------------------- Cámaras virtuales
            var camAstronaut = VirtualCamera(ctx, cameras, "CM_Gallery_01_Astronaut", AstronautStation + new Vector3(2.2f, 1.5f, -3.4f),
                Target(ctx, environment, "Look_Astronaut", AstronautStation + new Vector3(0f, 1.15f, 0f)), 38f, noise, 0.4f);
            var camAura = VirtualCamera(ctx, cameras, "CM_Gallery_02_AURA", AuraStation + new Vector3(0.6f, 1.6f, -2.6f),
                Target(ctx, environment, "Look_AURA", AuraStation + new Vector3(0f, 1.75f, 0f)), 40f, noise, 0.3f);
            var camExterior = ExteriorDolly(ctx, cameras, Target(ctx, environment, "Look_Base", ExteriorStation + new Vector3(4f, 3f, 0f)), out var dollyObject);
            var camControl = VirtualCamera(ctx, cameras, "CM_Gallery_04_ControlCenter", ControlStation + new Vector3(-4.8f, 1.8f, -3.6f),
                Target(ctx, environment, "Look_Control", ControlStation + new Vector3(0.5f, 1.3f, 1f)), 50f, noise, 0.3f);
            var camPower = VirtualCamera(ctx, cameras, "CM_Gallery_05_PowerRoom", PowerStation + new Vector3(3.6f, 1.8f, -3.3f),
                Target(ctx, environment, "Look_Power", PowerStation + new Vector3(-1.5f, 1.3f, 1.8f)), 52f, noise, 0.3f);
            var camShelter = VirtualCamera(ctx, cameras, "CM_Gallery_06_Shelter", ShelterStation + new Vector3(-2.6f, 1.7f, -2.4f),
                Target(ctx, environment, "Look_Shelter", ShelterStation + new Vector3(0.6f, 1f, 1.2f)), 55f, noise, 0.3f);
            var camEarth = VirtualCamera(ctx, cameras, "CM_Gallery_07_EarthControl", EarthStation + new Vector3(0f, 2.4f, -5.3f),
                Target(ctx, environment, "Look_Earth", EarthStation + new Vector3(0f, 2.2f, 5f)), 50f, noise, 0.25f);
            var camMars = VirtualCamera(ctx, cameras, "CM_Gallery_08_Mars", SpaceStation + new Vector3(-40f, 25f, -165f),
                Target(ctx, environment, "Look_Mars", SpaceStation), 40f, null, 0f);
            var camSun = VirtualCamera(ctx, cameras, "CM_Gallery_09_Sun", SpaceStation + new Vector3(-180f, 50f, -60f),
                Target(ctx, environment, "Look_Sun", sunBody), 30f, null, 0f);

            // ---------------------------------------------------------------- Timeline
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(PhaseBPaths.GalleryTimeline) != null)
                AssetDatabase.DeleteAsset(PhaseBPaths.GalleryTimeline);
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, PhaseBPaths.GalleryTimeline);

            var cameraGroup = timeline.CreateTrack<GroupTrack>(null, "CÁMARAS");
            var brainTrack = timeline.CreateTrack<CinemachineTrack>(cameraGroup, CinematicBindingKeys.Brain);

            var actorGroup = timeline.CreateTrack<GroupTrack>(null, "ACTORES");
            var astronautTrack = timeline.CreateTrack<AnimationTrack>(actorGroup, CinematicBindingKeys.Astronaut);
            double t = Sequence(astronautTrack, ctx, 0.0, new[]
            {
                ("AC_Astronaut_Idle", 3.0), ("AC_Astronaut_Walk", 3.5), ("AC_Astronaut_LookAround", 5.0), ("AC_Astronaut_Gesture", 3.0),
                ("AC_Astronaut_WorkConsole", 3.0), ("AC_Astronaut_CheckWrist", 3.0), ("AC_Astronaut_Celebrate", 2.5), ("AC_Astronaut_Concern", 3.0),
            });
            AddShot(brainTrack, director, camAstronaut, 0.0, t);
            double shotStart = t; // corte seco entre estaciones (están muy separadas)

            var auraTrack = timeline.CreateTrack<AnimationTrack>(actorGroup, CinematicBindingKeys.Aura);
            t = Sequence(auraTrack, ctx, shotStart, new[]
            {
                ("AC_AURA_Appear", 2.0), ("AC_AURA_Idle", 4.0), ("AC_AURA_Speak", 4.5), ("AC_AURA_Alert", 4.0),
            });
            AddShot(brainTrack, director, camAura, shotStart, t - shotStart);
            shotStart = t;

            var propsGroup = timeline.CreateTrack<GroupTrack>(null, "PROPS");
            var dollyTrack = timeline.CreateTrack<AnimationTrack>(cameraGroup, camExterior.name);
            var dollyClip = EnsureDollyClip(ctx);
            var dollyTimelineClip = dollyTrack.CreateClip(dollyClip);
            dollyTimelineClip.start = shotStart;
            dollyTimelineClip.duration = 12.0;
            var solarTrack = timeline.CreateTrack<AnimationTrack>(propsGroup, "SolarArray");
            AddClip(solarTrack, ctx.Clip("AC_SolarArray_Track"), shotStart, 8.0);
            AddShot(brainTrack, director, camExterior, shotStart, 12.0);
            shotStart += 12.0;

            var emergencyTrack = timeline.CreateTrack<ActivationTrack>(propsGroup, "EmergencyLights");
            emergencyTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
            var emergencyClip = emergencyTrack.CreateDefaultClip();
            emergencyClip.start = shotStart + 3.5;
            emergencyClip.duration = 3.5;
            emergencyClip.displayName = "Emergencia ON";
            AddShot(brainTrack, director, camControl, shotStart, 7.0);
            shotStart += 7.0;

            var batteryTrack = timeline.CreateTrack<AnimationTrack>(propsGroup, "BatteryBank");
            AddClip(batteryTrack, ctx.Clip("AC_Battery_Drain"), shotStart + 0.5, 6.0);
            AddShot(brainTrack, director, camPower, shotStart, 7.0);
            shotStart += 7.0;
            AddShot(brainTrack, director, camShelter, shotStart, 6.0);
            shotStart += 6.0;
            AddShot(brainTrack, director, camEarth, shotStart, 7.0);
            shotStart += 7.0;
            AddShot(brainTrack, director, camMars, shotStart, 6.0);
            shotStart += 6.0;
            AddShot(brainTrack, director, camSun, shotStart, 5.5);

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.SetGenericBinding(brainTrack, brain);
            director.SetGenericBinding(dollyTrack, dollyObject.GetComponent<Animator>());
            Bind(director, astronautTrack, astronaut);
            Bind(director, auraTrack, aura);
            Bind(director, solarTrack, FindChild(ctx, "PF_Set_MarsSurface", environment, "BaseAurora/SolarArray"));
            Bind(director, batteryTrack, power != null ? power.transform.Find("BatteryBank")?.gameObject : null);
            var emergency = control != null ? control.transform.Find("EmergencyLights") : null;
            if (emergency != null)
                director.SetGenericBinding(emergencyTrack, emergency.gameObject);

            var serializedRoot = new SerializedObject(sceneRoot);
            serializedRoot.FindProperty("cinematicId").stringValue = GalleryId;
            serializedRoot.FindProperty("director").objectReferenceValue = director;
            serializedRoot.FindProperty("cinematicCamera").objectReferenceValue = camera;
            serializedRoot.ApplyModifiedPropertiesWithoutUndo();

            // ---------------------------------------------------------------- Ambiente
            var sky = AssetDatabase.LoadAssetAtPath<Material>(PhaseBPaths.Skies + "/M_Sky_MarsDay.mat");
            if (sky != null)
                RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.47f, 0.39f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.31f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.13f, 0.10f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.74f, 0.50f, 0.34f);
            RenderSettings.fogDensity = 0.0015f;
            var lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(CinematicEditorPaths.LightingSettings);
            if (lightingSettings != null)
                Lightmapping.lightingSettings = lightingSettings;

            if (transmission == null)
                ctx.Report.Warning(category, "No se pudo colocar el efecto de transmisión.");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PhaseBPaths.GalleryScene))
            {
                ctx.Report.Error(category, $"No se pudo guardar {PhaseBPaths.GalleryScene}.");
                return false;
            }

            Selection.activeGameObject = root;
            ctx.Report.Ok(category, $"Creada {PhaseBPaths.GalleryScene} ({timeline.duration:0} s, 9 planos): astronauta con 8 animaciones, " +
                                    "AURA con 4, exterior de Base Aurora con paneles animados y polvo, sala de control con emergencia, " +
                                    "sala de máquinas con baterías descargándose y chispas, refugio, Control Tierra, Marte y el Sol.");
            return true;
#endif
        }

#if SOL3_CINEMACHINE && SOL3_SPLINES
        static CinemachineCamera VirtualCamera(BuildContext ctx, Transform parent, string name, Vector3 position, Transform target,
            float fieldOfView, NoiseSettings noise, float amplitude)
        {
            var go = CinematicBuildKit.Obj(name, parent, ctx.Layer, position);
            var camera = go.AddComponent<CinemachineCamera>();
            camera.Lens.FieldOfView = fieldOfView;
            camera.Lens.NearClipPlane = 0.1f;
            camera.Lens.FarClipPlane = 1500f;
            camera.Target.TrackingTarget = target;
            go.AddComponent<CinemachineRotationComposer>();
            if (noise != null && amplitude > 0f)
            {
                var perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
                perlin.NoiseProfile = noise;
                perlin.AmplitudeGain = amplitude;
                perlin.FrequencyGain = 0.7f;
            }
            if (target != null)
                go.transform.rotation = Quaternion.LookRotation(target.position - position);
            return camera;
        }

        static CinemachineCamera ExteriorDolly(BuildContext ctx, Transform parent, Transform target, out GameObject cameraObject)
        {
            var path = CinematicBuildKit.Obj("Dolly_Gallery_03_Exterior", parent, ctx.Layer);
            var container = path.AddComponent<SplineContainer>();
            var spline = container.Spline;
            if (spline == null)
            {
                spline = new Spline();
                container.Spline = spline;
            }
            spline.Clear();
            spline.Add(new Unity.Mathematics.float3(-55f, 24f, -42f), TangentMode.AutoSmooth);
            spline.Add(new Unity.Mathematics.float3(-6f, 15f, -60f), TangentMode.AutoSmooth);
            spline.Add(new Unity.Mathematics.float3(48f, 19f, -36f), TangentMode.AutoSmooth);

            cameraObject = CinematicBuildKit.Obj("CM_Gallery_03_Exterior", parent, ctx.Layer, new Vector3(-55f, 24f, -42f));
            var camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Lens.FieldOfView = 42f;
            camera.Lens.NearClipPlane = 0.3f;
            camera.Lens.FarClipPlane = 1500f;
            camera.Target.TrackingTarget = target;
            var dolly = cameraObject.AddComponent<CinemachineSplineDolly>();
            dolly.Spline = container;
            dolly.PositionUnits = PathIndexUnit.Normalized;
            dolly.CameraPosition = 0f;
            cameraObject.AddComponent<CinemachineRotationComposer>();
            cameraObject.AddComponent<Animator>();
            CinematicBuildKit.AddBindingTarget(cameraObject, cameraObject.name, null);
            return camera;
        }

        static AnimationClip EnsureDollyClip(BuildContext ctx)
        {
            var clip = new AnimationClip { name = "AC_Gallery_ExteriorDolly", frameRate = 30f };
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(CinemachineSplineDolly), "m_SplineSettings.Position"),
                AnimationCurve.EaseInOut(0f, 0f, 12f, 1f));
            return CinematicBuildKit.SaveClip(ctx, clip, PhaseBPaths.GalleryAnimations);
        }

        static void AddShot(CinemachineTrack track, PlayableDirector director, CinemachineCamera camera, double start, double duration)
        {
            var clip = track.CreateClip<CinemachineShot>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = camera.name;
            var shot = (CinemachineShot)clip.asset;
            shot.DisplayName = camera.name;
            shot.VirtualCamera.exposedName = new PropertyName(System.Guid.NewGuid().ToString("N"));
            director.SetReferenceValue(shot.VirtualCamera.exposedName, camera);
            EditorUtility.SetDirty(shot);
        }
#endif

        /// <summary>Encadena clips con 0,3 s de fundido entre ellos. Devuelve el final del último.</summary>
        static double Sequence(AnimationTrack track, BuildContext ctx, double start, (string clip, double duration)[] clips)
        {
            double time = start;
            foreach (var entry in clips)
            {
                var clip = ctx.Clip(entry.clip);
                if (clip == null)
                {
                    ctx.Report.Warning("Galería", $"Falta la animación {entry.clip}.");
                    continue;
                }
                AddClip(track, clip, time, entry.duration);
                time += entry.duration - 0.3;
            }
            return time + 0.3;
        }

        static void AddClip(AnimationTrack track, AnimationClip clip, double start, double duration)
        {
            if (clip == null)
                return;
            var timelineClip = track.CreateClip(clip);
            timelineClip.start = start;
            timelineClip.duration = duration;
            timelineClip.displayName = clip.name.Replace("AC_", string.Empty);
        }

        static void Bind(PlayableDirector director, TrackAsset track, GameObject target)
        {
            if (target != null && target.TryGetComponent<Animator>(out var animator))
                director.SetGenericBinding(track, animator);
        }

        static GameObject Place(BuildContext ctx, string prefabName, Transform parent, Vector3 position, Vector3 euler) =>
            CinematicBuildKit.Nest(ctx, prefabName, parent, position, euler);

        static GameObject FindChild(BuildContext ctx, string prefabName, Transform parent, string path)
        {
            foreach (Transform child in parent)
            {
                if (child.name != prefabName)
                    continue;
                var found = child.Find(path);
                return found != null ? found.gameObject : null;
            }
            return null;
        }

        static Transform Target(BuildContext ctx, Transform parent, string name, Vector3 position) =>
            CinematicBuildKit.Obj(name, parent, ctx.Layer, position).transform;

        static void Platform(BuildContext ctx, Transform parent, string name, Vector3 position)
        {
            CinematicBuildKit.Prim(PrimitiveType.Cylinder, name, parent, ctx.Mat("M_Base_Steel"), ctx.Layer,
                position + new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(5f, 0.05f, 5f));
            CinematicBuildKit.MeshObj(name + "_Ring", parent, ctx.MeshAsset("MSH_Ring_Unit"), ctx.Mat("M_Emit_Cyan"), ctx.Layer,
                position + new Vector3(0f, 0.11f, 0f), Vector3.zero, Vector3.one * 4.6f, false);
        }
    }
}

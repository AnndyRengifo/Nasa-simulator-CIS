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
    /// Genera la escena CIN_Template: una secuencia de 10 s que fija el estándar técnico de todas
    /// las cinemáticas (renderer 3D, capa aislada, Cinemachine + Timeline, enlaces por convención,
    /// luces en tiempo real, post-procesado y pista de audio) y sirve de prueba de humo de la Fase A.
    /// Los objetos son marcadores de posición (primitivas), no modelos finales.
    /// </summary>
    internal static class CinematicTemplateBuilder
    {
        public const string TemplateId = "template";

        const double DollyEnd = 6.0;
        const double CloseStart = 4.5;
        const double TotalDuration = 10.0;
        const double AlertStart = 6.5;

        public static bool Build(int layer, int rendererIndex, bool overwrite, CinematicReport report)
        {
            const string category = "Plantilla";
#if !(SOL3_CINEMACHINE && SOL3_SPLINES)
            report.Error(category, "Cinemachine o Splines aún no están instalados. Espera a que Unity resuelva los paquetes y ejecuta de nuevo la configuración.");
            return false;
#else
            if (!overwrite && AssetDatabase.LoadAssetAtPath<SceneAsset>(CinematicEditorPaths.TemplateScene) != null)
            {
                report.Ok(category, "La escena CIN_Template ya existía (usa 'Reconstruir escena de plantilla' para regenerarla).");
                return true;
            }

            if (layer < 0 || rendererIndex < 0)
            {
                report.Error(category, "Falta la capa Cinematic3D o el renderer 3D: no se puede crear la plantilla.");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.Warning(category, "Cancelado: había escenas con cambios sin guardar. Vuelve a ejecutar la configuración.");
                return false;
            }

            // La escena nueva se abre ANTES de crear o cargar assets: abrir una escena en modo Single
            // descarga de memoria los assets no referenciados (p. ej. un Timeline recién creado).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int mask = 1 << layer;

            var materials = EnsureMaterials(report);
            var dollyAnimation = EnsureDollyClip(overwrite);
            var hoverAnimation = EnsureHoverClip(overwrite);
            var timeline = CreateTimelineAsset();
            var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CinematicEditorPaths.DefaultVolumeProfile);
            var noiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(CinematicEditorPaths.NoiseMild)
                               ?? AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset");
            var lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(CinematicEditorPaths.LightingSettings);

            // ---------------------------------------------------------------- Jerarquía estándar
            var root = NewObject("[CINEMATIC_ROOT]", null, layer);
            var director = root.AddComponent<PlayableDirector>();
            var sceneRoot = root.AddComponent<CinematicSceneRoot>();

            var camerasGroup = NewObject("Cameras", null, layer).transform;
            var lightingGroup = NewObject("Lighting", null, layer).transform;
            var environmentGroup = NewObject("Environment", null, layer).transform;
            var actorsGroup = NewObject("Actors", null, layer).transform;
            var audioGroup = NewObject("Audio", null, layer).transform;

            // ---------------------------------------------------------------- Entorno (marcadores)
            var ground = NewPrimitive(PrimitiveType.Plane, "PH_Ground", environmentGroup, materials.Ground, layer);
            Place(ground, Vector3.zero, Vector3.zero, new Vector3(20f, 1f, 20f));

            Place(NewPrimitive(PrimitiveType.Cube, "PH_Habitat", environmentGroup, materials.Steel, layer),
                new Vector3(0f, 1.5f, 4f), new Vector3(0f, 20f, 0f), new Vector3(8f, 3f, 4f));
            Place(NewPrimitive(PrimitiveType.Sphere, "PH_Dome", environmentGroup, materials.Panel, layer),
                new Vector3(-4.5f, 0.6f, 7.5f), Vector3.zero, new Vector3(6f, 4f, 6f));
            Place(NewPrimitive(PrimitiveType.Cylinder, "PH_CommsTower", environmentGroup, materials.Steel, layer),
                new Vector3(5f, 3f, 7f), Vector3.zero, new Vector3(0.8f, 3f, 0.8f));
            Place(NewPrimitive(PrimitiveType.Sphere, "PH_Dish", environmentGroup, materials.Panel, layer),
                new Vector3(5f, 6.4f, 7f), new Vector3(-30f, 0f, 0f), new Vector3(2.4f, 0.35f, 2.4f));

            var rocks = new[]
            {
                new Vector4(-9f, 0.3f, -2f, 1.4f), new Vector4(-6f, 0.2f, -8f, 0.9f), new Vector4(11f, 0.4f, 1f, 1.8f),
                new Vector4(8f, 0.15f, -12f, 0.7f), new Vector4(-13f, 0.5f, 9f, 2.2f), new Vector4(14f, 0.3f, 12f, 1.5f),
            };
            for (int i = 0; i < rocks.Length; i++)
            {
                var rock = rocks[i];
                Place(NewPrimitive(PrimitiveType.Sphere, $"PH_Rock_{i + 1:00}", environmentGroup, materials.Rock, layer),
                    new Vector3(rock.x, rock.y, rock.z), new Vector3(0f, i * 47f, 0f), new Vector3(rock.w, rock.w * 0.6f, rock.w * 0.8f));
            }

            var habitatLookTarget = NewObject("LookTarget_Habitat", environmentGroup, layer);
            habitatLookTarget.transform.position = new Vector3(0f, 2f, 4f);

            // ---------------------------------------------------------------- Actor de prueba (sustituible)
            var actor = NewObject("TestActor", actorsGroup, layer);
            actor.transform.position = new Vector3(4f, 1.4f, -3f);
            var actorAnimator = actor.AddComponent<Animator>();
            var actorVisual = NewObject("Visual", actor.transform, layer);
            Place(NewPrimitive(PrimitiveType.Cube, "PH_AuraCore", actorVisual.transform, materials.Hologram, layer),
                Vector3.zero, new Vector3(45f, 0f, 45f), Vector3.one * 0.6f);
            AddBindingTarget(actor, "TestActor", actorVisual.transform);

            // ---------------------------------------------------------------- Iluminación
            var sunObject = NewObject("Sun", lightingGroup, layer);
            sunObject.transform.rotation = Quaternion.Euler(28f, -35f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = CinematicProjectSetup.Hex("#FFD9B3");
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Hard;
            sun.cullingMask = mask;
            sun.lightmapBakeType = LightmapBakeType.Realtime;

            var alertObject = NewObject("AlertLight", lightingGroup, layer);
            alertObject.transform.position = new Vector3(4f, 3.2f, -3f);
            var alertLight = alertObject.AddComponent<Light>();
            alertLight.type = LightType.Point;
            alertLight.color = CinematicProjectSetup.Hex("#DB6262");
            alertLight.intensity = 6f;
            alertLight.range = 10f;
            alertLight.shadows = LightShadows.None;
            alertLight.cullingMask = mask;
            alertLight.lightmapBakeType = LightmapBakeType.Realtime;
            AddBindingTarget(alertObject, "AlertLight", null);
            alertObject.SetActive(false);

            var volumeObject = NewObject("GlobalVolume", lightingGroup, layer);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = volumeProfile;

            // ---------------------------------------------------------------- Cámara física + Brain
            var cameraObject = NewObject("CinematicCamera", camerasGroup, layer);
            cameraObject.transform.position = new Vector3(-16f, 5f, -18f);
            var cinematicCamera = cameraObject.AddComponent<Camera>();
            cinematicCamera.clearFlags = CameraClearFlags.Skybox;
            cinematicCamera.cullingMask = mask;
            cinematicCamera.fieldOfView = 45f;
            cinematicCamera.nearClipPlane = 0.1f;
            cinematicCamera.farClipPlane = 1500f;
            cinematicCamera.allowMSAA = false;

            var cameraData = cinematicCamera.GetUniversalAdditionalCameraData();
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
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.5f);
            AddBindingTarget(cameraObject, CinematicBindingKeys.Brain, null);

            // ---------------------------------------------------------------- Plano 1: dolly abierto
            var dollyObject = NewObject("Dolly_Template_Wide", camerasGroup, layer);
            var dollyPath = dollyObject.AddComponent<SplineContainer>();
            var spline = dollyPath.Spline;
            if (spline == null)
            {
                spline = new Spline();
                dollyPath.Spline = spline;
            }
            spline.Clear();
            spline.Add(new Unity.Mathematics.float3(-16f, 5f, -18f), TangentMode.AutoSmooth);
            spline.Add(new Unity.Mathematics.float3(-6f, 3.5f, -15f), TangentMode.AutoSmooth);
            spline.Add(new Unity.Mathematics.float3(5f, 2.6f, -11f), TangentMode.AutoSmooth);

            var wideObject = NewObject("CM_Template_Wide", camerasGroup, layer);
            var wideCamera = wideObject.AddComponent<CinemachineCamera>();
            wideCamera.Lens.FieldOfView = 45f;
            wideCamera.Lens.NearClipPlane = 0.1f;
            wideCamera.Lens.FarClipPlane = 1500f;
            wideCamera.Target.TrackingTarget = habitatLookTarget.transform;
            var dolly = wideObject.AddComponent<CinemachineSplineDolly>();
            dolly.Spline = dollyPath;
            dolly.PositionUnits = PathIndexUnit.Normalized;
            dolly.CameraPosition = 0f;
            wideObject.AddComponent<CinemachineRotationComposer>();
            var wideAnimator = wideObject.AddComponent<Animator>();
            AddBindingTarget(wideObject, "CM_Template_Wide", null);

            // ---------------------------------------------------------------- Plano 2: cerrado con cámara en mano
            var closeObject = NewObject("CM_Template_Close", camerasGroup, layer);
            closeObject.transform.position = new Vector3(8.5f, 2.2f, -8.5f);
            var closeCamera = closeObject.AddComponent<CinemachineCamera>();
            closeCamera.Lens.FieldOfView = 28f;
            closeCamera.Lens.NearClipPlane = 0.1f;
            closeCamera.Lens.FarClipPlane = 1500f;
            closeCamera.Target.TrackingTarget = actor.transform;
            closeObject.AddComponent<CinemachineRotationComposer>();
            var handheld = closeObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
            handheld.NoiseProfile = noiseProfile;
            handheld.AmplitudeGain = 0.6f;
            handheld.FrequencyGain = 0.8f;

            // ---------------------------------------------------------------- Audio
            var musicObject = NewObject("Music", audioGroup, layer);
            var music = musicObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            AddBindingTarget(musicObject, CinematicBindingKeys.Music, null);

            // ---------------------------------------------------------------- Timeline
            var camerasTrackGroup = timeline.CreateTrack<GroupTrack>(null, "CÁMARAS");
            var brainTrack = timeline.CreateTrack<CinemachineTrack>(camerasTrackGroup, CinematicBindingKeys.Brain);
            var wideShot = CreateShot(brainTrack, director, wideCamera, 0.0, DollyEnd);
            var closeShot = CreateShot(brainTrack, director, closeCamera, CloseStart, TotalDuration - CloseStart);
            wideShot.blendOutDuration = DollyEnd - CloseStart;
            closeShot.blendInDuration = DollyEnd - CloseStart;

            var dollyTrack = timeline.CreateTrack<AnimationTrack>(camerasTrackGroup, wideObject.name);
            var dollyClip = dollyTrack.CreateClip(dollyAnimation);
            dollyClip.start = 0.0;
            dollyClip.duration = DollyEnd;
            dollyClip.displayName = dollyAnimation.name;

            var actorsTrackGroup = timeline.CreateTrack<GroupTrack>(null, "ACTORES");
            var actorTrack = timeline.CreateTrack<AnimationTrack>(actorsTrackGroup, actor.name);
            var hoverClip = actorTrack.CreateClip(hoverAnimation);
            hoverClip.start = 0.0;
            hoverClip.duration = TotalDuration;
            hoverClip.displayName = hoverAnimation.name;

            var lightingTrackGroup = timeline.CreateTrack<GroupTrack>(null, "ILUMINACIÓN");
            var alertTrack = timeline.CreateTrack<ActivationTrack>(lightingTrackGroup, alertObject.name);
            alertTrack.postPlaybackState = ActivationTrack.PostPlaybackState.LeaveAsIs;
            var alertClip = alertTrack.CreateDefaultClip();
            alertClip.start = AlertStart;
            alertClip.duration = TotalDuration - AlertStart;
            alertClip.displayName = "Alerta activa";

            var audioTrackGroup = timeline.CreateTrack<GroupTrack>(null, "AUDIO");
            var musicTrack = timeline.CreateTrack<AudioTrack>(audioTrackGroup, CinematicBindingKeys.Music);

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.SetGenericBinding(brainTrack, brain);
            director.SetGenericBinding(dollyTrack, wideAnimator);
            director.SetGenericBinding(actorTrack, actorAnimator);
            director.SetGenericBinding(alertTrack, alertObject);
            director.SetGenericBinding(musicTrack, music);

            var serializedRoot = new SerializedObject(sceneRoot);
            serializedRoot.FindProperty("cinematicId").stringValue = TemplateId;
            serializedRoot.FindProperty("director").objectReferenceValue = director;
            serializedRoot.FindProperty("cinematicCamera").objectReferenceValue = cinematicCamera;
            serializedRoot.ApplyModifiedPropertiesWithoutUndo();

            // ---------------------------------------------------------------- Ambiente marciano
            ApplyRenderSettings(materials.Sky, sun);
            if (lightingSettings != null)
                Lightmapping.lightingSettings = lightingSettings;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, CinematicEditorPaths.TemplateScene))
            {
                report.Error(category, $"No se pudo guardar {CinematicEditorPaths.TemplateScene}.");
                return false;
            }

            Selection.activeGameObject = root;
            report.Ok(category, $"Creada {CinematicEditorPaths.TemplateScene} con {CinematicEditorPaths.TemplateTimeline} " +
                                $"({TotalDuration:0} s: dolly Cinemachine sobre spline, plano cerrado con ruido de cámara en mano, " +
                                "fundido entre planos, actor animado, luz de alerta, post-procesado). La escena queda abierta.");
            report.Info(category, "Los objetos de la plantilla son primitivas provisionales (prefijo PH_), no modelos finales.");
            return true;
#endif
        }

#if SOL3_CINEMACHINE && SOL3_SPLINES
        static TimelineClip CreateShot(CinemachineTrack track, PlayableDirector director, CinemachineCamera camera, double start, double duration)
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
            return clip;
        }

        static AnimationClip EnsureDollyClip(bool overwrite)
        {
            string path = CinematicEditorPaths.TemplateAnimations + "/AC_Template_DollyWide.anim";
            var clip = PrepareClip(path, overwrite);
            if (clip != null)
                return clip;

            clip = new AnimationClip { name = "AC_Template_DollyWide", frameRate = 30f };
            var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(CinemachineSplineDolly), "m_SplineSettings.Position");
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.EaseInOut(0f, 0f, (float)DollyEnd, 1f));
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
#endif

        static AnimationClip EnsureHoverClip(bool overwrite)
        {
            string path = CinematicEditorPaths.TemplateAnimations + "/AC_Template_ActorHover.anim";
            var clip = PrepareClip(path, overwrite);
            if (clip != null)
                return clip;

            float end = (float)TotalDuration;
            clip = new AnimationClip { name = "AC_Template_ActorHover", frameRate = 30f };

            var hover = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(end * 0.25f, 0.25f), new Keyframe(end * 0.5f, 0f),
                new Keyframe(end * 0.75f, 0.25f), new Keyframe(end, 0f));
            for (int i = 0; i < hover.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(hover, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(hover, i, AnimationUtility.TangentMode.ClampedAuto);
            }

            SetTransformCurve(clip, "m_LocalPosition.x", AnimationCurve.Constant(0f, end, 0f));
            SetTransformCurve(clip, "m_LocalPosition.y", hover);
            SetTransformCurve(clip, "m_LocalPosition.z", AnimationCurve.Constant(0f, end, 0f));
            SetTransformCurve(clip, "localEulerAnglesRaw.x", AnimationCurve.Constant(0f, end, 0f));
            SetTransformCurve(clip, "localEulerAnglesRaw.y", AnimationCurve.Linear(0f, 0f, end, 360f));
            SetTransformCurve(clip, "localEulerAnglesRaw.z", AnimationCurve.Constant(0f, end, 0f));

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static void SetTransformCurve(AnimationClip clip, string property, AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Visual", typeof(Transform), property), curve);

        /// <summary>Devuelve el clip existente, o null si hay que crearlo (borrándolo antes si se reconstruye).</summary>
        static AnimationClip PrepareClip(string path, bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
                return null;
            if (!overwrite)
                return existing;
            AssetDatabase.DeleteAsset(path);
            return null;
        }

        static TimelineAsset CreateTimelineAsset()
        {
            // Siempre se regenera: sus pistas deben enlazarse con los objetos de la escena nueva.
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(CinematicEditorPaths.TemplateTimeline) != null)
                AssetDatabase.DeleteAsset(CinematicEditorPaths.TemplateTimeline);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, CinematicEditorPaths.TemplateTimeline);
            return timeline;
        }

        // -------------------------------------------------------------------- Materiales provisionales

        sealed class TemplateMaterials
        {
            public Material Ground;
            public Material Rock;
            public Material Steel;
            public Material Panel;
            public Material Hologram;
            public Material Sky;
        }

        static TemplateMaterials EnsureMaterials(CinematicReport report)
        {
            return new TemplateMaterials
            {
                Ground = EnsureLit("M_PH_MarsGround", "#B8693F", 0f, 0.15f, null, report),
                Rock = EnsureLit("M_PH_MarsRock", "#6E3B26", 0f, 0.1f, null, report),
                Steel = EnsureLit("M_PH_SteelBlue", "#30465D", 0.35f, 0.55f, null, report),
                Panel = EnsureLit("M_PH_SoftWhite", "#E8EFF4", 0.1f, 0.45f, null, report),
                Hologram = EnsureLit("M_PH_HologramCyan", "#53C7D8", 0f, 0.8f, 2.2f, report),
                Sky = EnsureSkybox("M_PH_Sky_MarsDay", report),
            };
        }

        static Material EnsureLit(string name, string hex, float metallic, float smoothness, float? emissionIntensity, CinematicReport report)
        {
            string path = $"{CinematicEditorPaths.PlaceholderMaterials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                report.Error("Materiales", "No se encontró el shader 'Universal Render Pipeline/Lit'.");
                return null;
            }

            var color = CinematicProjectSetup.Hex(hex);
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emissionIntensity.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emissionIntensity.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material EnsureSkybox(string name, CinematicReport report)
        {
            string path = $"{CinematicEditorPaths.PlaceholderMaterials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                report.Warning("Materiales", "No se encontró el shader 'Skybox/Procedural'; la plantilla usará el cielo por defecto.");
                return null;
            }

            material = new Material(shader) { name = name };
            material.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            material.SetFloat("_SunSize", 0.03f);
            material.SetFloat("_SunSizeConvergence", 6f);
            material.SetFloat("_AtmosphereThickness", 1.7f);
            material.SetColor("_SkyTint", new Color(0.86f, 0.58f, 0.40f));
            material.SetColor("_GroundColor", new Color(0.32f, 0.20f, 0.14f));
            material.SetFloat("_Exposure", 1.05f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void ApplyRenderSettings(Material sky, Light sun)
        {
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
            RenderSettings.fogDensity = 0.012f;
        }

        // -------------------------------------------------------------------- Utilidades de escena

        static GameObject NewObject(string name, Transform parent, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject NewPrimitive(PrimitiveType type, string name, Transform parent, Material material, int layer)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = layer;
            if (go.TryGetComponent<Collider>(out var collider))
                Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            if (material != null && go.TryGetComponent<MeshRenderer>(out var meshRenderer))
                meshRenderer.sharedMaterial = material;
            return go;
        }

        static void Place(GameObject go, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
        {
            var transform = go.transform;
            transform.localPosition = localPosition;
            transform.localEulerAngles = localEulerAngles;
            transform.localScale = localScale;
        }

        static CinematicBindingTarget AddBindingTarget(GameObject go, string key, Transform visualRoot)
        {
            var target = go.AddComponent<CinematicBindingTarget>();
            var serialized = new SerializedObject(target);
            serialized.FindProperty("bindingKey").stringValue = key;
            serialized.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }
    }
}

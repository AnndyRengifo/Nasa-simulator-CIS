using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sol3.Cinematics
{
    /// <summary>
    /// CONTRATO DE ESCENA. Cada escena cinemática (CIN_&lt;id&gt;.unity) contiene exactamente un
    /// CinematicSceneRoot. El sistema de reproducción solo habla con este componente (preparar,
    /// reproducir, omitir, detener, avisar del final) y nunca con los objetos internos de la escena:
    /// la escena puede rehacerse por completo sin tocar la lógica del juego.
    ///
    /// Si abres la escena sola en el Editor y pulsas Play, se reproduce como vista previa.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SOL-3/Cinemáticas/Cinematic Scene Root")]
    public sealed class CinematicSceneRoot : MonoBehaviour
    {
        enum State
        {
            Idle,
            Prepared,
            Playing,
            Finished,
        }

        [Tooltip("Identificador que implementa esta escena. Debe coincidir con su definición en el catálogo.")]
        [CinematicId, SerializeField] string cinematicId = string.Empty;

        [Tooltip("Director que reproduce el Timeline de la cinemática.")]
        [SerializeField] PlayableDirector director;

        [Tooltip("Cámara de la cinemática (con CinemachineBrain y AudioListener).")]
        [SerializeField] Camera cinematicCamera;

        [Tooltip("Si abres esta escena sola en el Editor y pulsas Play, se reproduce automáticamente.")]
        [SerializeField] bool autoPlayWhenStandalone = true;

        [Tooltip("Segundo del Timeline al que salta la omisión. Negativo = final del Timeline.")]
        [SerializeField] float skipToTime = -1f;

        [Tooltip("Asigna la capa Cinematic3D a todos los objetos de la escena al prepararla (red de seguridad).")]
        [SerializeField] bool enforceCinematicLayer = true;

        [Header("Vista previa (solo al abrir la escena sola)")]
        [Tooltip("Subtítulos que se muestran en la vista previa. En el juego los toma el gestor del catálogo.")]
        [SerializeField] SubtitleSheet previewSubtitles;

        [Tooltip("Relación de aspecto de composición de la vista previa (bandas negras).")]
        [Min(1f), SerializeField] float previewAspect = 2f;

        static readonly List<CinematicSceneRoot> s_ActiveRoots = new List<CinematicSceneRoot>();
        static int s_ExternalControlCount;

        State m_State = State.Idle;

        /// <summary>Raíces cinemáticas cargadas en este momento.</summary>
        public static IReadOnlyList<CinematicSceneRoot> ActiveRoots => s_ActiveRoots;

        /// <summary>Se dispara en Awake de cada raíz (el gestor lo usa para encontrar la escena recién cargada).</summary>
        public static event Action<CinematicSceneRoot> RootAwakened;

        /// <summary>True mientras un gestor externo controla la reproducción (se desactiva la vista previa automática).</summary>
        public static bool IsUnderExternalControl => s_ExternalControlCount > 0;

        /// <summary>El gestor lo llama ANTES de cargar una escena cinemática.</summary>
        public static void BeginExternalControl() => s_ExternalControlCount++;

        /// <summary>El gestor lo llama cuando ha terminado con la escena.</summary>
        public static void EndExternalControl() => s_ExternalControlCount = Mathf.Max(0, s_ExternalControlCount - 1);

        /// <summary>Se dispara una sola vez cuando la cinemática termina, se omite, se detiene o falla.</summary>
        public event Action<CinematicSceneRoot, CinematicEndReason> Finished;

        public string CinematicId => cinematicId;
        public PlayableDirector Director => director;
        public Camera CinematicCamera => cinematicCamera;
        public bool IsPrepared => m_State == State.Prepared;
        public bool IsPlaying => m_State == State.Playing;
        public bool IsFinished => m_State == State.Finished;

        /// <summary>Duración del Timeline asignado, en segundos.</summary>
        public double Duration => director != null && director.playableAsset != null ? director.playableAsset.duration : 0d;

        /// <summary>Tiempo actual del Timeline, en segundos (los subtítulos se sincronizan con este valor).</summary>
        public double CurrentTime => director != null ? director.time : 0d;

        string DisplayId => string.IsNullOrEmpty(cinematicId) ? gameObject.scene.name : cinematicId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Necesario si "Enter Play Mode Options" desactiva la recarga de dominio.
            s_ActiveRoots.Clear();
            s_ExternalControlCount = 0;
            RootAwakened = null;
        }

        void Reset()
        {
            director = GetComponentInChildren<PlayableDirector>(true);
            cinematicCamera = GetComponentInChildren<Camera>(true);
        }

        void Awake()
        {
            if (director == null)
                director = GetComponentInChildren<PlayableDirector>(true);

            if (director != null)
            {
                if (director.state == PlayState.Playing)
                    director.Stop();
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                // Hold mantiene el último fotograma hasta que el gestor haga el fundido de salida.
                director.extrapolationMode = DirectorWrapMode.Hold;
            }

            s_ActiveRoots.Add(this);
            RootAwakened?.Invoke(this);
        }

        void OnEnable()
        {
            if (director != null)
                director.stopped += OnDirectorStopped;
        }

        void OnDisable()
        {
            if (director != null)
                director.stopped -= OnDirectorStopped;
        }

        void Start()
        {
            if (!autoPlayWhenStandalone || IsUnderExternalControl || m_State != State.Idle)
                return;

            Debug.Log($"[Cinematics] Vista previa independiente de '{DisplayId}'.", this);
            if (Prepare(null, null))
            {
                CinematicSubtitlePresenter.Create(transform, previewAspect).Bind(director, previewSubtitles);
                Play();
            }
            else
                Complete(CinematicEndReason.Failed);
        }

        void Update()
        {
            if (m_State != State.Playing || director == null)
                return;

            // Con WrapMode.Hold el director no emite 'stopped' al llegar al final: lo detectamos aquí.
            double duration = Duration;
            if (duration <= 0d || director.time >= duration - 0.0001d)
                Complete(CinematicEndReason.Completed);
        }

        void OnDestroy()
        {
            s_ActiveRoots.Remove(this);
        }

        /// <summary>
        /// Prepara la escena: capa, sustituciones de modelos, Timeline opcional y enlaces por convención.
        /// Deja visible el primer fotograma. Devuelve false si falta el director o el Timeline.
        /// </summary>
        public bool Prepare(TimelineAsset timelineOverride, IReadOnlyList<CastOverride> castOverrides)
        {
            if (m_State == State.Playing || m_State == State.Finished)
            {
                Debug.LogWarning($"[Cinematics] '{DisplayId}' ya está en curso o terminada; no se puede preparar de nuevo.", this);
                return false;
            }

            if (director == null)
            {
                Debug.LogError($"[Cinematics] '{DisplayId}': falta el PlayableDirector en el CinematicSceneRoot.", this);
                return false;
            }

            if (enforceCinematicLayer)
                CinematicLayers.AssignToScene(gameObject.scene);

            ApplyCastOverrides(castOverrides);

            if (timelineOverride != null)
                director.playableAsset = timelineOverride;

            if (!(director.playableAsset is TimelineAsset))
            {
                Debug.LogError($"[Cinematics] '{DisplayId}': el PlayableDirector no tiene un Timeline asignado.", this);
                return false;
            }

            CinematicTimelineBinder.BindByConvention(director, gameObject.scene, true);
            director.RebuildGraph();
            director.time = 0d;
            director.Evaluate();
            m_State = State.Prepared;
            return true;
        }

        /// <summary>Reproduce el Timeline. Si no se había preparado, se prepara sin sustituciones.</summary>
        public void Play()
        {
            if (m_State == State.Playing || m_State == State.Finished)
                return;

            if (m_State == State.Idle && !Prepare(null, null))
            {
                Complete(CinematicEndReason.Failed);
                return;
            }

            m_State = State.Playing;
            director.Play();
        }

        /// <summary>Salta al punto de omisión, aplica ese fotograma y termina con motivo 'Skipped'.</summary>
        public void Skip()
        {
            if (m_State != State.Playing && m_State != State.Prepared)
                return;

            double target = Duration;
            if (skipToTime >= 0f)
                target = Math.Min(skipToTime, target);

            director.time = target;
            director.Evaluate();
            Complete(CinematicEndReason.Skipped);
        }

        /// <summary>Detiene la cinemática y termina con motivo 'Stopped'.</summary>
        public void StopPlayback()
        {
            if (m_State == State.Finished)
                return;
            Complete(CinematicEndReason.Stopped);
        }

        void OnDirectorStopped(PlayableDirector stoppedDirector)
        {
            // Solo ocurre si alguien llama a director.Stop() desde fuera.
            if (m_State == State.Playing)
                Complete(CinematicEndReason.Stopped);
        }

        void Complete(CinematicEndReason reason)
        {
            if (m_State == State.Finished)
                return;

            m_State = State.Finished;
            if (director != null && director.state == PlayState.Playing)
                director.Pause(); // conserva el último fotograma, sin saltos

            if (!IsUnderExternalControl)
                Debug.Log($"[Cinematics] Vista previa de '{DisplayId}' terminada ({reason}).", this);

            Finished?.Invoke(this, reason);
        }

        void ApplyCastOverrides(IReadOnlyList<CastOverride> castOverrides)
        {
            if (castOverrides == null || castOverrides.Count == 0)
                return;

            var targets = GetSceneTargets();
            for (int i = 0; i < castOverrides.Count; i++)
            {
                var cast = castOverrides[i];
                if (cast == null || cast.visualPrefab == null || string.IsNullOrEmpty(cast.bindingKey))
                    continue;

                CinematicBindingTarget match = null;
                for (int t = 0; t < targets.Count; t++)
                {
                    if (targets[t].BindingKey == cast.bindingKey)
                    {
                        match = targets[t];
                        break;
                    }
                }

                if (match == null)
                    Debug.LogWarning($"[Cinematics] '{DisplayId}': no existe ningún actor con la clave '{cast.bindingKey}' para sustituir su modelo.", this);
                else
                    match.ReplaceVisual(cast.visualPrefab);
            }
        }

        List<CinematicBindingTarget> GetSceneTargets()
        {
            var result = new List<CinematicBindingTarget>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<CinematicBindingTarget>(true));
            return result;
        }
    }
}

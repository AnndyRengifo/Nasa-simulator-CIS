using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Reproductor central de cinemáticas (implementa <see cref="ICinematicService"/>).
    ///
    /// Flujo de cada reproducción:
    ///  1. Fundido a negro.
    ///  2. El juego 2D pausa y guarda su estado (ICinematicGameBridge.OnCinematicStarting);
    ///     opcionalmente Time.timeScale = 0 y pausa del audio del juego.
    ///  3. Carga aditiva de la escena 3D de la definición (CIN_&lt;id&gt;), con límite de tiempo.
    ///  4. Preparación (Timeline de sustitución, modelos de sustitución, enlaces por convención),
    ///     desactivación de las cámaras 2D, subtítulos en el idioma activo y volúmenes.
    ///  5. Reproducción con botón "Omitir" (y botón Atrás en Android) cuando la definición lo permite,
    ///     y un límite de seguridad por si el Timeline no terminara.
    ///  6. Fundido a negro, descarga de la escena y liberación de recursos.
    ///  7. Restauración del juego 2D (OnCinematicFinished) o carga de la escena de retorno.
    ///  8. Fundido de vuelta y aviso de finalización (onFinished, exactamente una vez).
    ///
    /// Cualquier fallo (escena ausente, fuera de la build, sin CinematicSceneRoot, sin Timeline,
    /// carga demasiado lenta) termina con <see cref="CinematicEndReason.Failed"/> y la partida continúa.
    /// El gestor nunca decide ni modifica el resultado de la partida.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SOL-3/Cinemáticas/Cinematic Manager")]
    public sealed class CinematicManager : MonoBehaviour, ICinematicService
    {
        [Tooltip("Configuración central (Assets/Cinematics3D/Config/CinematicCatalog.asset).")]
        [SerializeField] CinematicCatalog catalog;

        [Tooltip("Componente del juego que implementa ICinematicGameBridge (por ejemplo SimpleCinematicGameBridge).")]
        [SerializeField] MonoBehaviour gameBridge;

        [Tooltip("Mantener el gestor entre escenas (DontDestroyOnLoad).")]
        [SerializeField] bool persistAcrossScenes = true;

        [Tooltip("Idioma de subtítulos. Vacío = idioma por defecto del catálogo.")]
        [SerializeField] string language = string.Empty;

        [SerializeField] bool verboseLogs = true;

        sealed class PauseState
        {
            public Scene PreviousActive;
            public bool TimeScaleChanged;
            public float TimeScale = 1f;
            public bool AudioChanged;
            public bool AudioPaused;
            public readonly List<Behaviour> Disabled = new List<Behaviour>();
        }

        CinematicOverlay m_Overlay;
        CinematicDefinition m_Definition;
        CinematicSceneRoot m_Root;
        PauseState m_Pause;
        bool m_SkipRequested;
        bool m_StopRequested;
        float m_Elapsed;

        /// <summary>Gestor activo (null si no hay ninguno en la escena).</summary>
        public static CinematicManager Instance { get; private set; }

        public bool IsPlaying { get; private set; }
        public string CurrentCinematicId { get; private set; } = string.Empty;
        public event Action<string> CinematicStarted;
        public event Action<CinematicResult> CinematicFinished;

        public CinematicCatalog Catalog
        {
            get => catalog;
            set => catalog = value;
        }

        /// <summary>Puente con el juego 2D. Puede asignarse por código.</summary>
        public ICinematicGameBridge Bridge { get; set; }

        public string Language
        {
            get => !string.IsNullOrEmpty(language) ? language : catalog != null ? catalog.Globals.defaultLanguage : "es";
            set => language = value ?? string.Empty;
        }

        /// <summary>True si en este momento se puede omitir la cinemática en curso.</summary>
        public bool CanSkip => IsPlaying && m_Definition != null && m_Definition.Skippable && m_Root != null && m_Root.IsPlaying
                               && m_Elapsed >= m_Definition.SkipAvailableAfter;

        /// <summary>Crea un gestor por código (alternativa a colocarlo en una escena).</summary>
        public static CinematicManager Create(CinematicCatalog catalog, ICinematicGameBridge bridge)
        {
            var go = new GameObject("CinematicManager");
            go.SetActive(false);
            var manager = go.AddComponent<CinematicManager>();
            manager.catalog = catalog;
            go.SetActive(true);
            manager.Bridge = bridge;
            return manager;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Cinematics] Ya existe un CinematicManager; se elimina el duplicado.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            if (gameBridge != null)
            {
                Bridge = gameBridge as ICinematicGameBridge;
                if (Bridge == null)
                    Debug.LogWarning($"[Cinematics] '{gameBridge.GetType().Name}' no implementa ICinematicGameBridge.", this);
            }

            if (catalog == null)
                Debug.LogWarning("[Cinematics] El CinematicManager no tiene catálogo asignado.", this);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (IsPlaying && m_Pause != null)
                RestoreGame(m_Pause);
        }

        // ================================================================== API pública

        public bool Play(string cinematicId, Action<CinematicResult> onFinished = null)
        {
            if (catalog == null)
            {
                Debug.LogError("[Cinematics] No se puede reproducir: falta el catálogo.", this);
                return false;
            }
            if (!catalog.TryGet(cinematicId, out var definition))
            {
                Debug.LogWarning($"[Cinematics] Identificador desconocido '{cinematicId}': petición rechazada.", this);
                return false;
            }
            return PlayDefinition(definition, onFinished);
        }

        /// <summary>Reproduce una definición concreta (útil para pruebas). Mismas reglas que Play.</summary>
        public bool PlayDefinition(CinematicDefinition definition, Action<CinematicResult> onFinished = null)
        {
            if (definition == null)
                return false;
            if (IsPlaying)
            {
                Log($"Petición de '{definition.Id}' rechazada: ya se está reproduciendo '{CurrentCinematicId}'.", true);
                return false;
            }
            if (catalog == null)
            {
                Debug.LogError("[Cinematics] No se puede reproducir: falta el catálogo.", this);
                return false;
            }
            if (definition.PlayOncePerSave && Bridge != null && Bridge.HasCinematicBeenShown(definition.Id))
            {
                Log($"'{definition.Id}' ya se mostró en esta partida: no se repite.", false);
                return false;
            }

            IsPlaying = true;
            CurrentCinematicId = definition.Id;
            StartCoroutine(PlayRoutine(definition, onFinished));
            return true;
        }

        public bool Skip()
        {
            if (!CanSkip)
                return false;
            m_SkipRequested = true;
            return true;
        }

        public void Stop()
        {
            if (IsPlaying)
                m_StopRequested = true;
        }

        // ================================================================== Reproducción

        IEnumerator PlayRoutine(CinematicDefinition definition, Action<CinematicResult> onFinished)
        {
            var globals = catalog.Globals;
            m_Definition = definition;
            m_Root = null;
            m_SkipRequested = false;
            m_StopRequested = false;
            m_Elapsed = 0f;
            EnsureOverlay(globals);
            m_Overlay.SetFadeColor(definition.FadeColor);
            m_Overlay.SetSkip(false, null);
            Log($"Inicio de '{definition.Id}'.", false);
            Safe(() => CinematicStarted?.Invoke(definition.Id));

            // 1-2. Fundido y pausa del juego 2D.
            yield return m_Overlay.FadeTo(1f, definition.FadeOutDuration);
            Safe(() => Bridge?.OnCinematicStarting(definition.Id));
            m_Pause = PauseGame(globals);

            // 3. Carga aditiva.
            var reason = CinematicEndReason.Completed;
            string message = null;
            Scene scene = default;
            CinematicSceneRoot.BeginExternalControl();

            if (!definition.Scene.IsAssigned)
            {
                reason = CinematicEndReason.Failed;
                message = "la definición no tiene escena asignada";
            }
            else if (!definition.Scene.IsInBuild)
            {
                reason = CinematicEndReason.Failed;
                message = $"la escena '{definition.Scene.SceneName}' no está en la lista de escenas de la build";
            }
            else
            {
                AsyncOperation load = null;
                try
                {
                    load = SceneManager.LoadSceneAsync(definition.Scene.ScenePath, LoadSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    message = exception.Message;
                }

                if (load == null)
                {
                    reason = CinematicEndReason.Failed;
                    message ??= "no se pudo iniciar la carga de la escena";
                }
                else
                {
                    float timer = 0f;
                    while (!load.isDone && timer < globals.sceneLoadTimeout)
                    {
                        timer += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    scene = SceneManager.GetSceneByPath(definition.Scene.ScenePath);
                    if (!load.isDone || !scene.IsValid() || !scene.isLoaded)
                    {
                        reason = CinematicEndReason.Failed;
                        message = "la escena tardó demasiado en cargar";
                        while (!load.isDone)
                            yield return null; // espera para poder descargarla limpiamente
                        scene = SceneManager.GetSceneByPath(definition.Scene.ScenePath);
                    }
                }
            }

            // 4. Preparación.
            if (reason != CinematicEndReason.Failed && !m_StopRequested)
            {
                m_Root = FindRoot(scene);
                if (m_Root == null)
                {
                    reason = CinematicEndReason.Failed;
                    message = "la escena no contiene un CinematicSceneRoot";
                }
                else
                {
                    if (!string.IsNullOrEmpty(m_Root.CinematicId) && m_Root.CinematicId != definition.Id)
                        Log($"La escena '{scene.name}' declara el identificador '{m_Root.CinematicId}' pero se reproduce como '{definition.Id}'.", true);
                    SceneManager.SetActiveScene(scene);
                    if (!m_Root.Prepare(definition.TimelineOverride, definition.CastOverrides))
                    {
                        reason = CinematicEndReason.Failed;
                        message = "no se pudo preparar el Timeline de la escena";
                    }
                }
            }

            // 5. Reproducción.
            if (reason != CinematicEndReason.Failed && m_Root != null && !m_StopRequested)
            {
                DisableGameCameras(m_Pause, scene, globals);
                ApplyAudio(scene, globals);
                var subtitles = m_Overlay.Subtitles;
                subtitles.gameObject.SetActive(true);
                subtitles.TargetAspect = globals.targetAspect;
                subtitles.VoiceVolume = CinematicAudioSettings.Master * CinematicAudioSettings.Voice;
                subtitles.Bind(m_Root.Director, definition.GetSubtitles(Language, globals.fallbackLanguage));

                bool finished = false;
                var rootReason = CinematicEndReason.Completed;
                void OnRootFinished(CinematicSceneRoot root, CinematicEndReason why)
                {
                    finished = true;
                    rootReason = why;
                }

                m_Root.Finished += OnRootFinished;
                m_Root.Play();
                yield return m_Overlay.FadeTo(0f, definition.FadeInDuration);

                float limit = Mathf.Max(definition.ExpectedDuration, (float)m_Root.Duration) + globals.watchdogMargin;
                while (!finished)
                {
                    m_Elapsed += Time.unscaledDeltaTime;
                    bool canSkip = CanSkip;
                    m_Overlay.SetSkip(canSkip, () => m_SkipRequested = true);
                    if (canSkip && BackButtonPressed())
                        m_SkipRequested = true;

                    if (m_SkipRequested && canSkip)
                    {
                        m_Root.Skip();
                    }
                    else if (m_StopRequested)
                    {
                        m_Root.StopPlayback();
                    }
                    else if (m_Elapsed > limit)
                    {
                        message = "límite de seguridad alcanzado";
                        Log($"'{definition.Id}' superó su duración prevista: se finaliza.", true);
                        m_Root.StopPlayback();
                    }
                    yield return null;
                }

                m_Root.Finished -= OnRootFinished;
                reason = rootReason;
                m_Overlay.SetSkip(false, null);
                yield return m_Overlay.FadeTo(1f, definition.FadeOutDuration);
            }
            else if (m_StopRequested && reason != CinematicEndReason.Failed)
            {
                reason = CinematicEndReason.Stopped;
            }

            // 6. Descarga y liberación de recursos.
            m_Overlay.Subtitles.Bind(null, null);
            m_Overlay.Subtitles.gameObject.SetActive(false);
            m_Root = null;
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.GetActiveScene() == scene && m_Pause.PreviousActive.IsValid() && m_Pause.PreviousActive.isLoaded)
                    SceneManager.SetActiveScene(m_Pause.PreviousActive);
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;
            }
            CinematicSceneRoot.EndExternalControl();
            if (globals.unloadUnusedAssetsAfterPlayback)
            {
                var cleanup = Resources.UnloadUnusedAssets();
                while (cleanup != null && !cleanup.isDone)
                    yield return null;
            }

            // 7. Vuelta al juego.
            RestoreGame(m_Pause);
            m_Pause = null;
            var result = new CinematicResult(definition.Id, reason, message);
            if (reason == CinematicEndReason.Failed)
                Debug.LogWarning($"[Cinematics] '{definition.Id}' no se pudo reproducir: {message}. La partida continúa.", this);
            if (result.WasShown)
                Safe(() => Bridge?.MarkCinematicShown(definition.Id));
            Safe(() => Bridge?.OnCinematicFinished(result));

            if (definition.EndBehaviour == CinematicEndBehaviour.LoadScene)
            {
                if (definition.ReturnScene.IsInBuild)
                {
                    var load = SceneManager.LoadSceneAsync(definition.ReturnScene.ScenePath, LoadSceneMode.Single);
                    while (load != null && !load.isDone)
                        yield return null;
                }
                else
                {
                    Debug.LogWarning($"[Cinematics] La escena de retorno de '{definition.Id}' no está en la build; se vuelve al juego.", this);
                }
            }

            // 8. Fundido de vuelta y aviso.
            yield return m_Overlay.FadeTo(0f, definition.FadeInDuration);
            IsPlaying = false;
            CurrentCinematicId = string.Empty;
            m_Definition = null;
            Log($"Fin de '{definition.Id}': {reason}{(string.IsNullOrEmpty(message) ? string.Empty : $" ({message})")}.", false);
            Safe(() => onFinished?.Invoke(result));
            Safe(() => CinematicFinished?.Invoke(result));
        }

        // ================================================================== Juego 2D

        PauseState PauseGame(CinematicGlobalSettings globals)
        {
            var state = new PauseState { PreviousActive = SceneManager.GetActiveScene() };
            if (globals.pauseWithTimeScale)
            {
                state.TimeScale = Time.timeScale;
                state.TimeScaleChanged = true;
                Time.timeScale = 0f;
            }
            if (globals.pauseGameAudio)
            {
                state.AudioPaused = AudioListener.pause;
                state.AudioChanged = true;
                AudioListener.pause = true;
            }
            return state;
        }

        void DisableGameCameras(PauseState state, Scene cinematicScene, CinematicGlobalSettings globals)
        {
            if (!globals.disableGameCameras)
                return;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
            {
                if (camera.enabled && camera.gameObject.scene != cinematicScene)
                {
                    camera.enabled = false;
                    state.Disabled.Add(camera);
                }
            }
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
            {
                if (listener.enabled && listener.gameObject.scene != cinematicScene)
                {
                    listener.enabled = false;
                    state.Disabled.Add(listener);
                }
            }
        }

        static void RestoreGame(PauseState state)
        {
            if (state == null)
                return;
            foreach (var behaviour in state.Disabled)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }
            state.Disabled.Clear();
            if (state.TimeScaleChanged)
                Time.timeScale = state.TimeScale;
            if (state.AudioChanged)
                AudioListener.pause = state.AudioPaused;
            state.TimeScaleChanged = false;
            state.AudioChanged = false;
        }

        // ================================================================== Audio

        void ApplyAudio(Scene scene, CinematicGlobalSettings globals)
        {
            float master = CinematicAudioSettings.Master;
            var mixer = globals.audioMixer;
            if (mixer != null)
            {
                mixer.SetFloat(globals.masterVolumeParameter, CinematicAudioSettings.ToDecibels(master));
                mixer.SetFloat(globals.musicVolumeParameter, CinematicAudioSettings.ToDecibels(CinematicAudioSettings.Music));
                mixer.SetFloat(globals.voiceVolumeParameter, CinematicAudioSettings.ToDecibels(CinematicAudioSettings.Voice));
                mixer.SetFloat(globals.sfxVolumeParameter, CinematicAudioSettings.ToDecibels(CinematicAudioSettings.Sfx));
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                {
                    source.ignoreListenerPause = true; // el juego 2D puede estar en pausa de audio
                    if (mixer != null && source.outputAudioMixerGroup != null)
                        continue;
                    bool music = source.name.IndexOf("Music", StringComparison.OrdinalIgnoreCase) >= 0
                                 || source.name.IndexOf("Ambience", StringComparison.OrdinalIgnoreCase) >= 0;
                    source.volume *= master * (music ? CinematicAudioSettings.Music : CinematicAudioSettings.Sfx);
                }
            }
        }

        // ================================================================== Utilidades

        void EnsureOverlay(CinematicGlobalSettings globals)
        {
            if (m_Overlay == null)
                m_Overlay = CinematicOverlay.Create(transform, globals.targetAspect, globals.skipLabel);
        }

        static CinematicSceneRoot FindRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<CinematicSceneRoot>(true);
                if (found != null)
                    return found;
            }
            return null;
        }

        static bool BackButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        void Safe(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        void Log(string message, bool warning)
        {
            if (warning)
                Debug.LogWarning("[Cinematics] " + message, this);
            else if (verboseLogs)
                Debug.Log("[Cinematics] " + message, this);
        }
    }
}

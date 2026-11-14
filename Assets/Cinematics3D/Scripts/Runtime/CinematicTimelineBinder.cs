using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
#if SOL3_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace Sol3.Cinematics
{
    /// <summary>
    /// Enlaza un Timeline con los objetos de su escena POR CONVENCIÓN DE NOMBRES, sin referencias
    /// rígidas. Respeta los enlaces ya hechos en la escena y solo completa los que faltan, lo que
    /// permite asignar un Timeline nuevo desde el catálogo (sustitución de Nivel 3).
    ///
    /// • Pistas: nombre de la pista == clave de un <see cref="CinematicBindingTarget"/>.
    /// • Clips Cinemachine: nombre del clip == nombre del GameObject de la CinemachineCamera.
    /// • Clips de Control Track: nombre del clip == nombre del GameObject controlado.
    /// </summary>
    public static class CinematicTimelineBinder
    {
        /// <summary>Completa los enlaces que falten. Devuelve cuántos enlaces se crearon.</summary>
        public static int BindByConvention(PlayableDirector director, Scene scene, bool logWarnings)
        {
            if (director == null)
                return 0;

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null)
                return 0;

            var targets = CollectTargets(scene, logWarnings);
            int created = 0;

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track == null)
                    continue;

                Type bindingType = GetBindingType(track);
                if (bindingType == null || director.GetGenericBinding(track) != null)
                    continue;

                if (!targets.TryGetValue(track.name, out var target))
                {
                    if (logWarnings)
                        Debug.LogWarning($"[Cinematics] La pista '{track.name}' no tiene enlace y no hay ningún CinematicBindingTarget con esa clave en la escena '{scene.name}'.", director);
                    continue;
                }

                var value = ResolveBindingValue(target, bindingType);
                if (value == null)
                {
                    if (logWarnings)
                        Debug.LogWarning($"[Cinematics] La pista '{track.name}' necesita un {bindingType.Name} en '{target.name}'.", target);
                    continue;
                }

                director.SetGenericBinding(track, value);
                created++;
            }

            created += ResolveClipReferences(director, timeline, scene, logWarnings);
            return created;
        }

        static Type GetBindingType(TrackAsset track)
        {
            foreach (var output in track.outputs)
            {
                if (output.outputTargetType != null)
                    return output.outputTargetType;
            }
            return null;
        }

        static UnityEngine.Object ResolveBindingValue(CinematicBindingTarget target, Type bindingType)
        {
            if (bindingType == typeof(GameObject))
                return target.gameObject;

            if (!typeof(Component).IsAssignableFrom(bindingType))
                return null;

            var component = target.GetComponent(bindingType);
            if (component == null && bindingType == typeof(Animator))
                component = target.gameObject.AddComponent<Animator>();
            return component;
        }

        static Dictionary<string, CinematicBindingTarget> CollectTargets(Scene scene, bool logWarnings)
        {
            var map = new Dictionary<string, CinematicBindingTarget>(StringComparer.Ordinal);
            if (!scene.IsValid() || !scene.isLoaded)
                return map;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var target in root.GetComponentsInChildren<CinematicBindingTarget>(true))
                {
                    string key = target.BindingKey;
                    if (string.IsNullOrEmpty(key))
                        continue;

                    // Las claves repetidas son normales (varias salas o actores en la misma escena):
                    // se usa la primera y solo importa si una pista sin enlazar depende de ella.
                    if (!map.ContainsKey(key))
                        map.Add(key, target);
                }
            }
            return map;
        }

        static Dictionary<string, GameObject> CollectObjectsByName(Scene scene)
        {
            var map = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            if (!scene.IsValid() || !scene.isLoaded)
                return map;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!map.ContainsKey(transform.name))
                        map.Add(transform.name, transform.gameObject);
                }
            }
            return map;
        }

        static int ResolveClipReferences(PlayableDirector director, TimelineAsset timeline, Scene scene, bool logWarnings)
        {
            Dictionary<string, GameObject> objectsByName = null;
            int resolved = 0;

            foreach (var track in timeline.GetOutputTracks())
            {
                foreach (var clip in track.GetClips())
                {
                    if (clip.asset is ControlPlayableAsset control)
                    {
                        if (IsResolved(director, control.sourceGameObject.exposedName))
                            continue;

                        objectsByName ??= CollectObjectsByName(scene);
                        if (objectsByName.TryGetValue(clip.displayName, out var controlled))
                        {
                            if (TrySetReference(director, control.sourceGameObject.exposedName, controlled, clip.displayName, logWarnings))
                                resolved++;
                        }
                        else if (logWarnings)
                        {
                            Debug.LogWarning($"[Cinematics] El clip de control '{clip.displayName}' no encuentra un objeto con ese nombre en '{scene.name}'.", director);
                        }
                        continue;
                    }
#if SOL3_CINEMACHINE
                    if (clip.asset is CinemachineShot shot)
                    {
                        if (IsResolved(director, shot.VirtualCamera.exposedName))
                            continue;

                        string cameraName = string.IsNullOrEmpty(shot.DisplayName) ? clip.displayName : shot.DisplayName;
                        objectsByName ??= CollectObjectsByName(scene);
                        if (objectsByName.TryGetValue(cameraName, out var cameraObject)
                            && cameraObject.TryGetComponent<CinemachineVirtualCameraBase>(out var virtualCamera))
                        {
                            if (TrySetReference(director, shot.VirtualCamera.exposedName, virtualCamera, cameraName, logWarnings))
                                resolved++;
                        }
                        else if (logWarnings)
                        {
                            Debug.LogWarning($"[Cinematics] El plano '{cameraName}' no encuentra una CinemachineCamera con ese nombre en '{scene.name}'.", director);
                        }
                    }
#endif
                }
            }
            return resolved;
        }

        static bool IsResolved(PlayableDirector director, PropertyName exposedName)
        {
            if (PropertyName.IsNullOrEmpty(exposedName))
                return false;
            var value = director.GetReferenceValue(exposedName, out bool isValid);
            return isValid && value != null;
        }

        static bool TrySetReference(PlayableDirector director, PropertyName exposedName, UnityEngine.Object value, string label, bool logWarnings)
        {
            if (PropertyName.IsNullOrEmpty(exposedName))
            {
                if (logWarnings)
                    Debug.LogWarning($"[Cinematics] El clip '{label}' no tiene referencia expuesta. Ábrelo en el Timeline y vuelve a asignar su objeto.", director);
                return false;
            }
            director.SetReferenceValue(exposedName, value);
            return true;
        }
    }
}

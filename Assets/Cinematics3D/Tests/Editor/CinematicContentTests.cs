using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sol3.Cinematics.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
#if SOL3_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace Sol3.Cinematics.Tests
{
    /// <summary>
    /// Pruebas de contenido (EditMode): cada una de las siete cinemáticas cumple el contrato de escena,
    /// tiene subtítulos válidos, respeta el presupuesto de Android y se puede re-enlazar por convención
    /// (base de la sustitución de Nivel 3).
    /// </summary>
    public class CinematicContentTests
    {
        const string CatalogPath = "Assets/Cinematics3D/Config/CinematicCatalog.asset";
        static IEnumerable<string> Ids => CinematicIds.All;

        static CinematicCatalog Catalog => AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CatalogPath);

        static CinematicDefinition Definition(string id)
        {
            var catalog = Catalog;
            Assert.IsNotNull(catalog, $"No existe {CatalogPath}.");
            Assert.IsTrue(catalog.TryGet(id, out var definition), $"El catálogo no contiene '{id}'.");
            return definition;
        }

        static Scene Open(CinematicDefinition definition)
        {
            Assert.IsTrue(definition.Scene.IsAssigned, $"'{definition.Id}' no tiene escena asignada.");
            return EditorSceneManager.OpenScene(definition.Scene.ScenePath, OpenSceneMode.Additive);
        }

        static void Close(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
        }

        static CinematicSceneRoot Root(Scene scene) =>
            scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<CinematicSceneRoot>(true)).FirstOrDefault(r => r != null);

        [Test]
        public void Catalog_ContainsTheSevenIdentifiersExactlyOnce()
        {
            var catalog = Catalog;
            Assert.IsNotNull(catalog);
            var ids = catalog.Definitions.Where(d => d != null).Select(d => d.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
            CollectionAssert.IsSubsetOf(CinematicIds.All.ToList(), ids);
        }

        [TestCaseSource(nameof(Ids))]
        public void Definition_HasSceneInBuildAndValidSubtitles(string id)
        {
            var definition = Definition(id);
            Assert.IsTrue(definition.Scene.IsAssigned, "Escena sin asignar.");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.Scene.ScenePath), "La escena no existe.");
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == definition.Scene.ScenePath), "La escena no está en la build.");
            Assert.IsTrue(definition.Skippable, "Toda cinemática debe poder omitirse.");

            var sheet = definition.GetSubtitles("es", "es");
            Assert.IsNotNull(sheet, "Falta la hoja de subtítulos en español.");
            Assert.AreEqual(id, sheet.CinematicId);
            var problems = new List<string>();
            Assert.IsTrue(sheet.Validate(problems), string.Join("\n", problems));
            Assert.Greater(sheet.Lines.Count, 0, "La hoja no tiene líneas.");
            foreach (var line in sheet.Lines)
                Assert.IsNotNull(line.speaker, $"La línea '{line.lineId}' no tiene personaje.");
        }

        [TestCaseSource(nameof(Ids))]
        public void Scene_FulfillsTheCinematicContract(string id)
        {
            var definition = Definition(id);
            var scene = Open(definition);
            try
            {
                var root = Root(scene);
                Assert.IsNotNull(root, "Falta el CinematicSceneRoot.");
                Assert.AreEqual(id, root.CinematicId);
                Assert.IsNotNull(root.Director, "Falta el PlayableDirector.");
                Assert.IsFalse(root.Director.playOnAwake, "El director no debe reproducirse solo.");
                var timeline = root.Director.playableAsset as TimelineAsset;
                Assert.IsNotNull(timeline, "El director no tiene Timeline.");
                Assert.Greater(timeline.duration, 0d);
                Assert.LessOrEqual(timeline.duration, definition.ExpectedDuration + 0.5f, "La duración prevista de la definición es menor que el Timeline.");

                foreach (var track in timeline.GetOutputTracks())
                {
                    if (track.outputs.Any(o => o.outputTargetType != null))
                        Assert.IsNotNull(root.Director.GetGenericBinding(track), $"La pista '{track.name}' no está enlazada.");
                }

                var camera = root.CinematicCamera;
                Assert.IsNotNull(camera, "Falta la cámara de la cinemática.");
                Assert.AreEqual(CinematicLayers.Mask, camera.cullingMask, "La cámara debe dibujar solo la capa Cinematic3D.");
                Assert.IsNotNull(camera.GetComponent<AudioListener>(), "La cámara debe tener AudioListener.");
#if SOL3_CINEMACHINE
                Assert.IsNotNull(camera.GetComponent<CinemachineBrain>(), "La cámara debe tener CinemachineBrain.");
                foreach (var track in timeline.GetOutputTracks().OfType<CinemachineTrack>())
                {
                    foreach (var clip in track.GetClips())
                    {
                        var shot = (CinemachineShot)clip.asset;
                        var camera3D = root.Director.GetReferenceValue(shot.VirtualCamera.exposedName, out bool valid);
                        Assert.IsTrue(valid && camera3D != null, $"El plano '{clip.displayName}' no tiene cámara.");
                    }
                }
#endif
                int layer = CinematicLayers.Index;
                foreach (var go in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
                    Assert.AreEqual(layer, go.layer, $"'{go.name}' no está en la capa Cinematic3D.");

                var sheet = definition.GetSubtitles("es", "es");
                foreach (var line in sheet.Lines)
                    Assert.LessOrEqual(line.endTime, timeline.duration + 0.01, $"La línea '{line.lineId}' termina después del Timeline.");
            }
            finally
            {
                Close(scene);
            }
        }

        [TestCaseSource(nameof(Ids))]
        public void Scene_RespectsTheAndroidBudget(string id)
        {
            var scene = Open(Definition(id));
            try
            {
                var stats = CinematicSceneStats.Measure(scene);
                var problems = stats.BudgetProblems().ToList();
                Assert.IsEmpty(problems, string.Join("\n", problems));
                var lighting = Lightmapping.GetLightingSettingsForScene(scene);
                if (lighting != null)
                    Assert.IsFalse(lighting.bakedGI, "Las cinemáticas usan solo iluminación en tiempo real.");
            }
            finally
            {
                Close(scene);
            }
        }

        [TestCaseSource(nameof(Ids))]
        public void Scene_RebindsByConvention_ForLevel3Replacement(string id)
        {
            var scene = Open(Definition(id));
            try
            {
                var root = Root(scene);
                var director = root.Director;
                var timeline = (TimelineAsset)director.playableAsset;
                var tracks = timeline.GetOutputTracks().Where(t => t.outputs.Any(o => o.outputTargetType != null)).ToList();
                foreach (var track in tracks)
                    director.ClearGenericBinding(track);

                CinematicTimelineBinder.BindByConvention(director, scene, false);

                foreach (var track in tracks)
                    Assert.IsNotNull(director.GetGenericBinding(track), $"La pista '{track.name}' no se re-enlaza por convención (falta un CinematicBindingTarget con esa clave).");
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void Characters_RespectTheTriangleBudget()
        {
            var astronaut = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cinematics3D/Prefabs/Characters/PF_Astronaut.prefab");
            var aura = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cinematics3D/Prefabs/Characters/PF_AURA.prefab");
            Assert.IsNotNull(astronaut);
            Assert.IsNotNull(aura);
            Assert.LessOrEqual(CinematicSceneStats.CountTriangles(astronaut), CinematicAndroidBudget.MaxAstronautTriangles, "Astronauta por encima del presupuesto.");
            Assert.LessOrEqual(CinematicSceneStats.CountTriangles(aura), CinematicAndroidBudget.MaxAuraTriangles, "AURA por encima del presupuesto.");
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sol3.Cinematics.Tests
{
    /// <summary>
    /// Pruebas de integración (PlayMode) del CinematicManager con un juego 2D simulado:
    /// cada cinemática pausa el juego, se reproduce, se omite y devuelve el control con el estado intacto;
    /// y los casos de error no bloquean la partida.
    /// </summary>
    public class CinematicManagerTests
    {
        sealed class TestBridge : ICinematicGameBridge
        {
            public readonly HashSet<string> Shown = new HashSet<string>();
            public int Started;
            public int Finished;
            public void OnCinematicStarting(string cinematicId) => Started++;
            public void OnCinematicFinished(CinematicResult result) => Finished++;
            public bool HasCinematicBeenShown(string cinematicId) => Shown.Contains(cinematicId);
            public void MarkCinematicShown(string cinematicId) => Shown.Add(cinematicId);
        }

        static IEnumerable<string> Ids => CinematicIds.All;

        CinematicCatalog m_Catalog;
        CinematicManager m_Manager;
        TestBridge m_Bridge;
        Camera m_GameCamera;
        AudioListener m_GameListener;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            m_Catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CinematicCatalog>("Assets/Cinematics3D/Config/CinematicCatalog.asset");
#endif
            if (m_Catalog == null)
                Assert.Ignore("No se encontró el catálogo (estas pruebas se ejecutan en el Editor).");

            Time.timeScale = 1f;
            AudioListener.pause = false;
            var game = new GameObject("Game2D_Camera");
            m_GameCamera = game.AddComponent<Camera>();
            m_GameCamera.cullingMask = ~CinematicLayers.Mask;
            m_GameListener = game.AddComponent<AudioListener>();
            m_Bridge = new TestBridge();
            m_Manager = CinematicManager.Create(m_Catalog, m_Bridge);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (m_Manager != null && m_Manager.IsPlaying)
            {
                m_Manager.Stop();
                yield return WaitFor(() => !m_Manager.IsPlaying, 30f);
            }
            if (m_Manager != null)
                UnityEngine.Object.DestroyImmediate(m_Manager.gameObject);
            if (m_GameCamera != null)
                UnityEngine.Object.DestroyImmediate(m_GameCamera.gameObject);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return null;
        }

        static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            float time = 0f;
            while (!condition() && time < timeout)
            {
                time += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Cinematic_PausesGame_Plays_Skips_AndRestores([ValueSource(nameof(Ids))] string id)
        {
            Assert.IsTrue(m_Catalog.TryGet(id, out var definition));
            CinematicResult? result = null;

            Assert.IsTrue(m_Manager.Play(id, r => result = r), "La petición debería aceptarse.");
            Assert.IsFalse(m_Manager.Play(id), "Una segunda petición simultánea debe rechazarse.");
            Assert.IsFalse(m_Manager.Play(CinematicIds.Intro == id ? CinematicIds.Sol1Night : CinematicIds.Intro), "No puede haber dos cinemáticas a la vez.");

            yield return WaitFor(() => m_Manager.CanSkip, 40f);
            Assert.IsTrue(m_Manager.CanSkip, "La cinemática no llegó a reproducirse (¿escena ausente?).");
            Assert.AreEqual(1, m_Bridge.Started, "El juego 2D debe ser avisado antes de empezar.");
            Assert.AreEqual(0f, Time.timeScale, "El juego 2D debe quedar pausado.");
            Assert.IsFalse(m_GameCamera.enabled, "La cámara 2D debe desactivarse durante la cinemática.");
            Assert.IsFalse(m_GameListener.enabled, "El AudioListener del juego debe desactivarse.");
            Assert.IsTrue(SceneManager.GetSceneByPath(definition.Scene.ScenePath).isLoaded, "La escena 3D debe estar cargada.");

            Assert.IsTrue(m_Manager.Skip(), "La cinemática debe poder omitirse.");
            yield return WaitFor(() => result.HasValue, 40f);

            Assert.IsTrue(result.HasValue, "onFinished debe invocarse.");
            Assert.AreEqual(CinematicEndReason.Skipped, result.Value.Reason);
            Assert.IsFalse(m_Manager.IsPlaying);
            Assert.AreEqual(1f, Time.timeScale, "Se debe restaurar Time.timeScale.");
            Assert.IsTrue(m_GameCamera.enabled, "Se debe reactivar la cámara 2D.");
            Assert.IsTrue(m_GameListener.enabled, "Se debe reactivar el AudioListener del juego.");
            Assert.IsFalse(AudioListener.pause, "Se debe reanudar el audio del juego.");
            Assert.IsFalse(SceneManager.GetSceneByPath(definition.Scene.ScenePath).isLoaded, "La escena 3D debe descargarse.");
            Assert.AreEqual(1, m_Bridge.Finished);
            Assert.IsTrue(m_Bridge.Shown.Contains(id), "Debe registrarse como vista.");
        }

        [UnityTest]
        public IEnumerator UnknownIdentifier_IsRejected_WithoutTouchingTheGame()
        {
            Assert.IsFalse(m_Manager.Play("id_inexistente"));
            yield return null;
            Assert.IsFalse(m_Manager.IsPlaying);
            Assert.AreEqual(0, m_Bridge.Started);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator MissingScene_Fails_AndGameContinues()
        {
            var missing = ScriptableObject.CreateInstance<CinematicDefinition>();
            CinematicResult? result = null;
            Assert.IsTrue(m_Manager.PlayDefinition(missing, r => result = r));
            yield return WaitFor(() => result.HasValue, 20f);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(CinematicEndReason.Failed, result.Value.Reason);
            Assert.IsFalse(result.Value.WasShown);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(m_GameCamera.enabled);
            Assert.AreEqual(1, m_Bridge.Finished, "El juego debe recuperar el control también si falla.");
            UnityEngine.Object.DestroyImmediate(missing);
        }

        [UnityTest]
        public IEnumerator PlayOncePerSave_IsRespected()
        {
            Assert.IsTrue(m_Catalog.TryGet(CinematicIds.Intro, out var intro));
            Assume.That(intro.PlayOncePerSave, "La intro está configurada como 'una vez por partida'.");
            m_Bridge.Shown.Add(CinematicIds.Intro);
            Assert.IsFalse(m_Manager.Play(CinematicIds.Intro), "Una cinemática ya vista no se repite en la misma partida.");
            yield return null;
            Assert.IsFalse(m_Manager.IsPlaying);
        }

        [UnityTest]
        public IEnumerator Stop_ReturnsControlToTheGame()
        {
            CinematicResult? result = null;
            Assert.IsTrue(m_Manager.Play(CinematicIds.Sol1Night, r => result = r));
            yield return WaitFor(() => m_Manager.CanSkip, 40f);
            m_Manager.Stop();
            yield return WaitFor(() => result.HasValue, 40f);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(CinematicEndReason.Stopped, result.Value.Reason);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(m_GameCamera.enabled);
        }
    }
}

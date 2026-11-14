using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Fase F — Pruebas y optimización.
    /// • "Optimizar y medir": regenera el astronauta con esferas de bajo coste y mide las siete escenas
    ///   frente al presupuesto de Android (Logs/SOL3_Cinematics_Performance.md).
    /// • "Ejecutar pruebas automáticas": lanza las pruebas EditMode (contenido) y PlayMode (integración);
    ///   los resultados quedan en Logs/SOL3_Cinematics_Tests.txt.
    /// </summary>
    internal static class CinematicPhaseF
    {
        const string Title = "SOL-3 · Cinemáticas · Fase F";
        public const string PerformanceLog = "Logs/SOL3_Cinematics_Performance.md";
        public const string TestsLog = "Logs/SOL3_Cinematics_Tests.txt";
        const string EditModeAssembly = "SOL3.Cinematics.Tests.Editor";
        const string PlayModeAssembly = "SOL3.Cinematics.Tests.Runtime";
        const string PendingPlayModeKey = "SOL3.Cinematics.PendingPlayModeTests";

        // ================================================================== Optimización y medición

        public static void OptimizeAndMeasure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog(Title, "Sal del modo Play y espera a que Unity termine de compilar.", "Entendido");
                return;
            }
            if (!EditorUtility.DisplayDialog(Title,
                    "1. Se regenerará el astronauta con esferas de 320 triángulos en lugar de 768 (mismo aspecto, mismos nombres y animaciones; las escenas no cambian).\n" +
                    "2. Se medirán las siete escenas frente al presupuesto de Android.\n\n¿Continuar?", "Continuar", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var report = new CinematicReport();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorUtility.DisplayProgressBar(Title, "Optimizando personajes…", 0.1f);
                var ctx = new BuildContext { Overwrite = true, Layer = CinematicLayers.Index, Report = report };
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CinematicPhaseC.LoadExisting(ctx);
                long before = Triangles("Assets/Cinematics3D/Prefabs/Characters/PF_Astronaut.prefab");
                CinematicCharacterBuilder.BuildAll(ctx);
                AssetDatabase.SaveAssets();
                long after = Triangles("Assets/Cinematics3D/Prefabs/Characters/PF_Astronaut.prefab");
                report.Ok("Optimización", $"Astronauta: {before:N0} → {after:N0} triángulos (presupuesto {CinematicAndroidBudget.MaxAstronautTriangles:N0}).");

                EditorUtility.DisplayProgressBar(Title, "Midiendo escenas…", 0.5f);
                string table = Measure(report);
                Directory.CreateDirectory("Logs");
                File.WriteAllText(PerformanceLog,
                    $"# Rendimiento de las cinemáticas ({DateTime.Now:yyyy-MM-dd HH:mm})\n\n" +
                    $"Astronauta: {after:N0} triángulos · AURA: {Triangles("Assets/Cinematics3D/Prefabs/Characters/PF_AURA.prefab"):N0} triángulos\n\n" +
                    table + "\n\nPresupuesto por escena: " +
                    $"{CinematicAndroidBudget.MaxSceneTriangles:N0} triángulos, {CinematicAndroidBudget.MaxPointLights} luces puntuales sin sombra, " +
                    $"{CinematicAndroidBudget.MaxParticles} partículas, {CinematicAndroidBudget.MaxTextureMegabytes} MB de texturas, " +
                    $"{CinematicAndroidBudget.MaxMaterials} materiales.\n", new UTF8Encoding(false));
                report.Info("Rendimiento", $"Tabla completa en {PerformanceLog}.");
            }
            catch (Exception exception)
            {
                report.Error("Fase F", exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            string log = report.Publish("Fase F · Optimización y rendimiento", "SOL3_Cinematics_PhaseF.txt");
            EditorUtility.DisplayDialog(Title, $"{report.Summary}\n\nDetalle en {log} y {PerformanceLog}.", "Cerrar");
        }

        static long Triangles(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab != null ? CinematicSceneStats.CountTriangles(prefab) : 0;
        }

        static string Measure(CinematicReport report)
        {
            var rows = new StringBuilder(CinematicSceneStats.TableHeader);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Se carga DESPUÉS de abrir la escena: abrirla descarga de memoria los assets cargados antes.
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            foreach (var id in CinematicIds.All)
            {
                if (!catalog.TryGet(id, out var definition) || !definition.Scene.IsAssigned)
                {
                    report.Error("Rendimiento", $"'{id}' no tiene escena.");
                    continue;
                }
                var scene = EditorSceneManager.OpenScene(definition.Scene.ScenePath, OpenSceneMode.Additive);
                var stats = CinematicSceneStats.Measure(scene);
                EditorSceneManager.CloseScene(scene, true);
                rows.Append('\n').Append(stats.ToRow());
                var problems = stats.BudgetProblems().ToList();
                if (problems.Count == 0)
                    report.Ok("Rendimiento", $"{stats.SceneName}: dentro del presupuesto ({stats.Triangles:N0} triángulos, {stats.PointLights} luces, {stats.MaxParticles} partículas máx.).");
                else
                    report.Warning("Rendimiento", $"{stats.SceneName}: {string.Join("; ", problems)}");
            }
            return rows.ToString();
        }

        // ================================================================== Pruebas automáticas

        public static void RunTests()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog(Title, "Sal del modo Play y espera a que Unity termine de compilar.", "Entendido");
                return;
            }
            if (!EditorUtility.DisplayDialog(Title,
                    "Se ejecutarán las pruebas automáticas:\n\n" +
                    "1. EditMode (contenido): contrato de escena, subtítulos, capa, enlaces por convención y presupuesto Android de las 7 cinemáticas.\n" +
                    "2. PlayMode (integración): cada cinemática pausa el juego, se reproduce, se omite y lo restaura; errores y casos límite.\n\n" +
                    "Unity entrará y saldrá de Play varias veces (≈2-3 minutos). No toques el Editor mientras tanto.",
                    "Ejecutar", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            File.WriteAllText(TestsLog, $"=== Pruebas automáticas SOL-3 · {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n", new UTF8Encoding(false));
            SessionState.SetBool(PendingPlayModeKey, true);
            Execute(TestMode.EditMode, EditModeAssembly);
        }

        static void Execute(TestMode mode, string assembly)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = mode, assemblyNames = new[] { assembly } }));
        }

        /// <summary>Escribe los resultados de nuestras pruebas en Logs/ y encadena PlayMode tras EditMode.</summary>
        [InitializeOnLoad]
        sealed class ResultsWriter : ICallbacks
        {
            static ResultsWriter() => TestRunnerApi.RegisterTestCallback(new ResultsWriter());

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var leaves = new List<ITestResultAdaptor>();
                Collect(result, leaves);
                leaves = leaves.Where(l => l.FullName.StartsWith("Sol3.Cinematics.Tests", StringComparison.Ordinal)).ToList();
                if (leaves.Count == 0)
                    return;

                int passed = leaves.Count(l => l.TestStatus == TestStatus.Passed);
                int failed = leaves.Count(l => l.TestStatus == TestStatus.Failed);
                int skipped = leaves.Count - passed - failed;
                bool playMode = leaves.Any(l => l.FullName.Contains("CinematicManagerTests"));
                var text = new StringBuilder();
                text.AppendLine();
                text.AppendLine($"--- {(playMode ? "PlayMode (integración)" : "EditMode (contenido)")}: {passed} superadas · {failed} fallidas · {skipped} omitidas ({DateTime.Now:HH:mm:ss}) ---");
                foreach (var leaf in leaves)
                {
                    string status = leaf.TestStatus == TestStatus.Passed ? "[OK]   " : leaf.TestStatus == TestStatus.Failed ? "[FALLO]" : "[OMIT] ";
                    text.AppendLine($"{status} {leaf.FullName.Replace("Sol3.Cinematics.Tests.", string.Empty)} ({leaf.Duration:0.0} s)");
                    if (leaf.TestStatus == TestStatus.Failed && !string.IsNullOrEmpty(leaf.Message))
                        text.AppendLine("        " + leaf.Message.Trim().Replace("\n", "\n        "));
                }
                File.AppendAllText(TestsLog, text.ToString(), new UTF8Encoding(false));
                string summary = $"[Cinematics] Pruebas {(playMode ? "PlayMode" : "EditMode")}: {passed} superadas, {failed} fallidas, {skipped} omitidas (detalle en {TestsLog}).";
                if (failed > 0)
                    Debug.LogError(summary);
                else
                    Debug.Log(summary);

                if (!playMode && SessionState.GetBool(PendingPlayModeKey, false))
                {
                    SessionState.SetBool(PendingPlayModeKey, false);
                    EditorApplication.delayCall += () => Execute(TestMode.PlayMode, PlayModeAssembly);
                }
            }

            static void Collect(ITestResultAdaptor node, List<ITestResultAdaptor> leaves)
            {
                if (node.HasChildren)
                {
                    foreach (var child in node.Children)
                        Collect(child, leaves);
                }
                else
                {
                    leaves.Add(node);
                }
            }
        }
    }
}

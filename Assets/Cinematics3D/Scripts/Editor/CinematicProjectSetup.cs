using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>
    /// Asistente de la Fase A. Es idempotente: puede ejecutarse varias veces y solo crea lo que falte.
    /// No modifica nada del juego 2D: añade un renderer 3D al final de la lista del URP Asset
    /// (el renderer por defecto sigue siendo el 2D), una capa nueva y assets dentro de Assets/Cinematics3D.
    /// </summary>
    internal static class CinematicProjectSetup
    {
        const string Title = "SOL-3 · Cinemáticas · Fase A";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        const string CinemachineNoisePresets = "Packages/com.unity.cinemachine/Presets/Noise/";

        internal readonly struct DefinitionSpec
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly float Duration;
            public readonly bool PlayOncePerSave;
            public readonly string Notes;

            public DefinitionSpec(string id, string displayName, float duration, bool playOncePerSave, string notes)
            {
                Id = id;
                DisplayName = displayName;
                Duration = duration;
                PlayOncePerSave = playOncePerSave;
                Notes = notes;
            }
        }

        /// <summary>Las siete cinemáticas con su duración objetivo (se ajustará al crear cada Timeline).</summary>
        internal static readonly DefinitionSpec[] Specs =
        {
            new DefinitionSpec(CinematicIds.Intro, "Introducción — Marte y Base Aurora", 75f, true,
                "Disparo: nueva partida. Marte → superficie → exterior de la base → interior → astronauta → AURA → juego 2D."),
            new DefinitionSpec(CinematicIds.Sol1Night, "Primera noche marciana", 30f, true,
                "Disparo: comienzo de la primera noche. Baja la luz, paneles solares, baterías, aviso de AURA."),
            new DefinitionSpec(CinematicIds.SolarAlert, "Alerta por actividad solar", 40f, true,
                "Disparo: la lógica del juego activa el evento solar. Sol, alerta en control, AURA analiza, refugio."),
            new DefinitionSpec(CinematicIds.Sol3Start, "Inicio del tercer sol", 40f, true,
                "Disparo: comienzo del tercer sol. Amanecer, recorrido por la base, preparación, objetivos finales."),
            new DefinitionSpec(CinematicIds.EndingSuccess, "Final — misión completada", 55f, false,
                "Disparo: SOLO cuando la lógica del juego determina la victoria. Informe, transmisión, confirmación de la Tierra."),
            new DefinitionSpec(CinematicIds.EndingFailure, "Final — fallo crítico", 45f, false,
                "Disparo: SOLO cuando la lógica del juego determina la derrota. Alertas, fallos, luces de emergencia."),
            new DefinitionSpec(CinematicIds.EndingPartial, "Final parcial — tripulación a salvo", 35f, false,
                "Disparo: SOLO cuando la lógica del juego determina el final parcial. Experimentos incompletos, sistemas estables."),
        };

        /// <summary>Ejecuta la configuración pidiendo confirmación y muestra el resultado.</summary>
        public static void RunInteractive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(Title, "Sal del modo Play antes de ejecutar la configuración.", "Entendido");
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog(Title, "Unity está compilando o importando. Espera a que termine y vuelve a intentarlo.", "Entendido");
                return;
            }

            const string message =
                "Se van a crear o completar (sin tocar el juego 2D):\n\n" +
                "• Carpetas en Assets/Cinematics3D\n" +
                "• Capa 'Cinematic3D'\n" +
                "• Universal Renderer 3D añadido al URP Asset (el 2D sigue siendo el de por defecto)\n" +
                "• Perfil de post-procesado y ajustes de iluminación en tiempo real\n" +
                "• Catálogo central con las 7 definiciones de cinemática\n" +
                "• Personajes de subtítulos (AURA, Control Tierra) y perfiles de ruido de cámara\n" +
                "• Recursos esenciales de TextMesh Pro (si faltan)\n" +
                "• Escena de plantilla CIN_Template (se abrirá; se te pedirá guardar la escena actual si tiene cambios)\n\n" +
                "Puedes ejecutarlo de nuevo cuando quieras: solo crea lo que falte.";

            if (!EditorUtility.DisplayDialog(Title, message, "Configurar", "Cancelar"))
                return;

            var report = Run();
            string setupLog = report.Publish("Configuración Fase A", "SOL3_Cinematics_Setup.txt");

            var validation = CinematicValidator.Validate();
            string validationLog = validation.Publish("Validación del sistema de cinemáticas", "SOL3_Cinematics_Validation.txt");

            EditorUtility.DisplayDialog(Title,
                $"Configuración: {report.Summary}\nValidación: {validation.Summary}\n\n" +
                "Los elementos PENDIENTES corresponden a fases posteriores (escenas y subtítulos de cada cinemática).\n\n" +
                $"Informes completos en la Consola y en:\n{setupLog}\n{validationLog}",
                "Cerrar");
        }

        /// <summary>Ejecuta todos los pasos sin diálogos de confirmación (salvo el de guardar escenas).</summary>
        public static CinematicReport Run()
        {
            var report = new CinematicReport();
            int layer = -1;
            int rendererIndex = -1;

            try
            {
                Progress("Carpetas", 0.05f);
                EnsureFolders(report);

                Progress("Capa Cinematic3D", 0.15f);
                layer = EnsureLayer(report);

                Progress("Renderer 3D", 0.25f);
                rendererIndex = EnsureRenderer(report);

                Progress("Post-procesado e iluminación", 0.35f);
                EnsureVolumeProfile(report);
                EnsureLightingSettings(report);

                Progress("Personajes de subtítulos", 0.45f);
                EnsureSpeakers(report);

                Progress("Catálogo central", 0.55f);
                EnsureCatalog(rendererIndex, report);

                Progress("Perfiles de cámara", 0.65f);
                EnsureNoiseProfiles(report);

                Progress("TextMesh Pro", 0.75f);
                EnsureTmpEssentials(report);

                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                report.Error("Configuración", "Excepción inesperada: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            try
            {
                CinematicTemplateBuilder.Build(layer, rendererIndex, false, report);
            }
            catch (Exception exception)
            {
                report.Error("Plantilla", "Excepción al crear la escena de plantilla: " + exception.Message);
                Debug.LogException(exception);
                // Descarta la escena a medio construir para que Unity no pida guardarla como "Untitled".
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                    UnityEditor.SceneManagement.NewSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            return report;
        }

        static void Progress(string step, float value) =>
            EditorUtility.DisplayProgressBar(Title, step + "…", value);

        // ------------------------------------------------------------------ Carpetas

        static void EnsureFolders(CinematicReport report)
        {
            int created = 0;
            foreach (var folder in CinematicEditorPaths.AllFolders)
            {
                if (EnsureFolder(folder))
                    created++;
            }
            report.Ok("Carpetas", created > 0
                ? $"Estructura Assets/Cinematics3D lista ({created} carpeta(s) nuevas)."
                : "Estructura Assets/Cinematics3D ya existía.");
        }

        internal static bool EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return false;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            return true;
        }

        // ------------------------------------------------------------------ Capa

        internal static int EnsureLayer(CinematicReport report)
        {
            int existing = LayerMask.NameToLayer(CinematicLayers.LayerName);
            if (existing >= 0)
            {
                report.Ok("Capa", $"La capa '{CinematicLayers.LayerName}' ya existe (índice {existing}).");
                return existing;
            }

            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                report.Error("Capa", "No se pudo abrir ProjectSettings/TagManager.asset.");
                return -1;
            }

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            var layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray)
            {
                report.Error("Capa", "El TagManager no tiene la lista de capas esperada.");
                return -1;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue))
                    continue;

                element.stringValue = CinematicLayers.LayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                report.Ok("Capa", $"Creada la capa '{CinematicLayers.LayerName}' en el índice {i}.");
                return i;
            }

            report.Error("Capa", "No queda ninguna capa de usuario libre (8-31).");
            return -1;
        }

        // ------------------------------------------------------------------ Renderer 3D

        internal static List<UniversalRenderPipelineAsset> CollectUrpAssets()
        {
            var assets = new List<UniversalRenderPipelineAsset>();

            void Add(RenderPipelineAsset candidate)
            {
                if (candidate is UniversalRenderPipelineAsset urp && !assets.Contains(urp))
                    assets.Add(urp);
            }

            Add(GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                Add(QualitySettings.GetRenderPipelineAssetAt(i));
            return assets;
        }

        static int EnsureRenderer(CinematicReport report)
        {
            const string category = "Render";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CinematicEditorPaths.RendererAsset);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                AssetDatabase.CreateAsset(rendererData, CinematicEditorPaths.RendererAsset);
                ResourceReloader.TryReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
                report.Ok(category, $"Creado el Universal Renderer 3D (Forward): {CinematicEditorPaths.RendererAsset}");
                if (rendererData.postProcessData == null)
                    report.Warning(category, "No se encontró el PostProcessData por defecto de URP: el post-procesado de las cinemáticas no funcionará hasta asignarlo en el renderer.");
            }
            else
            {
                report.Ok(category, "El Universal Renderer 3D ya existía.");
            }

            var urpAssets = CollectUrpAssets();
            if (urpAssets.Count == 0)
            {
                report.Error(category, "Ningún nivel de calidad usa un URP Asset: no se puede registrar el renderer 3D.");
                return -1;
            }

            int index = -1;
            foreach (var urp in urpAssets)
            {
                int assetIndex = AddRendererIfMissing(urp, rendererData, report);
                if (index < 0)
                    index = assetIndex;
                else if (assetIndex != index)
                    report.Warning(category, $"El renderer 3D tiene un índice distinto en '{urp.name}' ({assetIndex}); las cámaras usan {index}.");
            }
            return index;
        }

        static int AddRendererIfMissing(UniversalRenderPipelineAsset urp, ScriptableRendererData rendererData, CinematicReport report)
        {
            var serialized = new SerializedObject(urp);
            var list = serialized.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray)
            {
                report.Error("Render", $"'{urp.name}' no tiene la lista de renderers esperada (m_RendererDataList).");
                return -1;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == rendererData)
                {
                    report.Ok("Render", $"'{urp.name}': renderer 3D registrado en el índice {i}; el renderer por defecto sigue siendo el {DefaultRendererLabel(urp)}.");
                    return i;
                }
            }

            int index = list.arraySize;
            list.arraySize = index + 1;
            list.GetArrayElementAtIndex(index).objectReferenceValue = rendererData;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();
            report.Ok("Render", $"'{urp.name}': renderer 3D añadido en el índice {index}; el renderer por defecto sigue siendo el {DefaultRendererLabel(urp)}.");
            return index;
        }

        internal static string DefaultRendererLabel(UniversalRenderPipelineAsset urp)
        {
            var list = urp.rendererDataList;
            if (list.Length == 0 || list[0] == null)
                return "(vacío)";
            return list[0] is UniversalRendererData ? $"3D '{list[0].name}'" : $"2D '{list[0].name}'";
        }

        // ------------------------------------------------------------------ Post-procesado e iluminación

        static void EnsureVolumeProfile(CinematicReport report)
        {
            const string category = "Post-procesado";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(CinematicEditorPaths.DefaultVolumeProfile) != null)
            {
                report.Ok(category, "El perfil VP_Cinematic_Default ya existía.");
                return;
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, CinematicEditorPaths.DefaultVolumeProfile);

            var tonemapping = AddOverride<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);

            var colorAdjustments = AddOverride<ColorAdjustments>(profile);
            colorAdjustments.contrast.Override(8f);
            colorAdjustments.saturation.Override(4f);

            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            report.Ok(category, "Creado VP_Cinematic_Default (Tonemapping Neutral, Bloom a 1/4 de resolución, Color Adjustments, Vignette): coste apto para móvil.");
        }

        static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static void EnsureLightingSettings(CinematicReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<LightingSettings>(CinematicEditorPaths.LightingSettings) != null)
            {
                report.Ok("Iluminación", "LS_Cinematic_Realtime ya existía.");
                return;
            }

            var settings = new LightingSettings
            {
                name = "LS_Cinematic_Realtime",
                bakedGI = false,
                realtimeGI = false,
            };
            AssetDatabase.CreateAsset(settings, CinematicEditorPaths.LightingSettings);
            report.Ok("Iluminación", "Creado LS_Cinematic_Realtime: iluminación 100 % en tiempo real, sin lightmaps que hornear ni cargar en Android.");
        }

        // ------------------------------------------------------------------ Datos

        static void EnsureSpeakers(CinematicReport report)
        {
            EnsureSpeaker("SPK_AURA", "aura", "AURA", Hex("#53C7D8"), report);
            EnsureSpeaker("SPK_ControlTierra", "earth_control", "Control Tierra", Hex("#E8EFF4"), report);
        }

        static void EnsureSpeaker(string fileName, string speakerId, string displayName, Color color, CinematicReport report)
        {
            string path = $"{CinematicEditorPaths.Speakers}/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<CinematicSpeaker>(path) != null)
            {
                report.Ok("Personajes", $"{fileName} ya existía.");
                return;
            }

            var speaker = ScriptableObject.CreateInstance<CinematicSpeaker>();
            AssetDatabase.CreateAsset(speaker, path);
            var serialized = new SerializedObject(speaker);
            serialized.FindProperty("speakerId").stringValue = speakerId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("nameColor").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            report.Ok("Personajes", $"Creado {fileName} ('{displayName}').");
        }

        static void EnsureCatalog(int rendererIndex, CinematicReport report)
        {
            const string category = "Catálogo";
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicEditorPaths.Catalog);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CinematicCatalog>();
                AssetDatabase.CreateAsset(catalog, CinematicEditorPaths.Catalog);
                report.Ok(category, $"Creado el catálogo central: {CinematicEditorPaths.Catalog}");
            }
            else
            {
                report.Ok(category, "El catálogo central ya existía.");
            }

            var serialized = new SerializedObject(catalog);
            if (rendererIndex >= 0)
                serialized.FindProperty("globals.cinematicRendererIndex").intValue = rendererIndex;

            var definitions = serialized.FindProperty("definitions");
            int added = 0;
            foreach (var spec in Specs)
            {
                var definition = EnsureDefinition(spec, report);
                if (definition == null || ContainsReference(definitions, definition))
                    continue;

                int index = definitions.arraySize;
                definitions.arraySize = index + 1;
                definitions.GetArrayElementAtIndex(index).objectReferenceValue = definition;
                added++;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            if (added > 0)
                report.Ok(category, $"{added} definición(es) registradas en el catálogo.");
        }

        static CinematicDefinition EnsureDefinition(DefinitionSpec spec, CinematicReport report)
        {
            string path = CinematicEditorPaths.DefinitionFor(spec.Id);
            var definition = AssetDatabase.LoadAssetAtPath<CinematicDefinition>(path);
            if (definition != null)
                return definition;

            definition = ScriptableObject.CreateInstance<CinematicDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("notes").stringValue = spec.Notes;
            serialized.FindProperty("expectedDuration").floatValue = spec.Duration;
            serialized.FindProperty("playOncePerSave").boolValue = spec.PlayOncePerSave;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            report.Ok("Catálogo", $"Creada la definición '{spec.Id}' ({spec.Duration:0} s objetivo). Escena y subtítulos: pendientes de su fase.");
            return definition;
        }

        static bool ContainsReference(SerializedProperty array, UnityEngine.Object value)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == value)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ Cámaras

        static void EnsureNoiseProfiles(CinematicReport report)
        {
            CopyPackageAsset(CinemachineNoisePresets + "Handheld_normal_mild.asset", CinematicEditorPaths.NoiseMild, report);
            CopyPackageAsset(CinemachineNoisePresets + "Handheld_tele_mild.asset", CinematicEditorPaths.NoiseTele, report);
            CopyPackageAsset(CinemachineNoisePresets + "6D Wobble.asset", CinematicEditorPaths.NoiseWobble, report);
        }

        static void CopyPackageAsset(string source, string destination, CinematicReport report)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
            {
                report.Ok("Cámaras", $"{Path.GetFileName(destination)} ya existía.");
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(source) == null)
            {
                report.Warning("Cámaras", $"No se encontró {source} (¿Cinemachine sigue instalándose?).");
                return;
            }

            if (AssetDatabase.CopyAsset(source, destination))
                report.Ok("Cámaras", $"Perfil de ruido editable creado: {Path.GetFileName(destination)}");
            else
                report.Warning("Cámaras", $"No se pudo copiar {source}.");
        }

        // ------------------------------------------------------------------ TextMesh Pro

        static void EnsureTmpEssentials(CinematicReport report)
        {
            const string category = "TextMesh Pro";
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
            {
                report.Ok(category, "Los recursos esenciales de TextMesh Pro ya estaban importados.");
                return;
            }

            var ugui = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.ugui");
            if (ugui == null)
            {
                report.Error(category, "No se encontró el paquete com.unity.ugui (incluye TextMesh Pro).");
                return;
            }

            string package = Path.Combine(ugui.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(package))
            {
                report.Error(category, $"No se encontró {package}. Impórtalo desde Window > TextMeshPro > Import TMP Essential Resources.");
                return;
            }

            UnityEditor.AssetPackage.Package.Import(package, false);
            report.Ok(category, "Importados los recursos esenciales de TextMesh Pro (Assets/TextMesh Pro) para los subtítulos.");
        }

        internal static Color Hex(string html) =>
            ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.magenta;
    }
}

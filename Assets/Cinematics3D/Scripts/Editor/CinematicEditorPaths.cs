namespace Sol3.Cinematics.EditorTools
{
    /// <summary>Rutas y nombres de archivo del sistema de cinemáticas (una sola fuente de verdad).</summary>
    internal static class CinematicEditorPaths
    {
        public const string Root = "Assets/Cinematics3D";

        public const string Scenes = Root + "/Scenes";
        public const string Timelines = Root + "/Timelines";
        public const string Animations = Root + "/Animations";
        public const string Models = Root + "/Models";
        public const string Materials = Root + "/Materials";
        public const string Textures = Root + "/Textures";
        public const string Audio = Root + "/Audio";
        public const string Subtitles = Root + "/Subtitles";
        public const string Cameras = Root + "/Cameras";
        public const string Prefabs = Root + "/Prefabs";
        public const string Documentation = Root + "/Documentation";
        public const string Config = Root + "/Config";
        public const string Rendering = Root + "/Rendering";
        public const string Scripts = Root + "/Scripts";

        public const string Definitions = Config + "/Definitions";
        public const string Speakers = Config + "/Speakers";
        public const string NoiseProfiles = Cameras + "/Noise";
        public const string PlaceholderMaterials = Materials + "/Placeholders";
        public const string TemplateAnimations = Animations + "/Template";

        public const string Catalog = Config + "/CinematicCatalog.asset";
        public const string RendererAsset = Rendering + "/CinematicUniversalRenderer.asset";
        public const string DefaultVolumeProfile = Rendering + "/VP_Cinematic_Default.asset";
        public const string LightingSettings = Rendering + "/LS_Cinematic_Realtime.lighting";
        public const string NoiseMild = NoiseProfiles + "/NP_Handheld_Mild.asset";
        public const string NoiseTele = NoiseProfiles + "/NP_Handheld_Tele_Mild.asset";
        public const string NoiseWobble = NoiseProfiles + "/NP_Wobble.asset";

        public const string TemplateScene = Scenes + "/CIN_Template.unity";
        public const string TemplateTimeline = Timelines + "/TL_Template.playable";

        public const string Readme = Documentation + "/README_Cinematicas.md";

        /// <summary>Carpetas que crea la configuración (en orden de creación).</summary>
        public static readonly string[] AllFolders =
        {
            Root, Scenes, Timelines, Animations, TemplateAnimations, Models, Materials, PlaceholderMaterials,
            Textures, Audio, Subtitles, Cameras, NoiseProfiles, Prefabs, Documentation, Config, Definitions,
            Speakers, Rendering,
        };

        public static string SceneFor(string id) => $"{Scenes}/CIN_{id}.unity";
        public static string TimelineFor(string id) => $"{Timelines}/TL_{id}.playable";
        public static string DefinitionFor(string id) => $"{Definitions}/CinematicDef_{id}.asset";
        public static string SubtitlesFor(string id, string language) => $"{Subtitles}/SUB_{id}_{language}.asset";
    }
}

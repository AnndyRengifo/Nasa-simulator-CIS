using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>Muestra <see cref="SceneReference"/> como un selector de escenas con aviso de build.</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    internal sealed class SceneReferenceDrawer : PropertyDrawer
    {
        const float ButtonWidth = 120f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return NeedsBuildWarning(property, out _) ? line * 2f + EditorGUIUtility.standardVerticalSpacing : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var guidProperty = property.FindPropertyRelative("guid");
            var pathProperty = property.FindPropertyRelative("path");

            string path = ResolvePath(guidProperty, pathProperty);
            var current = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            var picked = (SceneAsset)EditorGUI.ObjectField(line, label, current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                string newPath = picked != null ? AssetDatabase.GetAssetPath(picked) : string.Empty;
                pathProperty.stringValue = newPath;
                guidProperty.stringValue = picked != null ? AssetDatabase.AssetPathToGUID(newPath) : string.Empty;
            }

            if (NeedsBuildWarning(property, out string warningPath))
            {
                var second = new Rect(position.x, line.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
                var labelRect = new Rect(second.x + EditorGUIUtility.labelWidth, second.y, second.width - EditorGUIUtility.labelWidth - ButtonWidth - 4f, second.height);
                var buttonRect = new Rect(second.xMax - ButtonWidth, second.y, ButtonWidth, second.height);
                EditorGUI.LabelField(labelRect, EditorGUIUtility.TrTextContentWithIcon("No está en la build", MessageType.Warning));
                if (GUI.Button(buttonRect, "Añadir a la build"))
                    AddToBuild(warningPath);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>El GUID manda: si la escena se movió o se renombró, actualiza la ruta guardada.</summary>
        static string ResolvePath(SerializedProperty guidProperty, SerializedProperty pathProperty)
        {
            string path = pathProperty.stringValue;
            if (!string.IsNullOrEmpty(guidProperty.stringValue))
            {
                string fromGuid = AssetDatabase.GUIDToAssetPath(guidProperty.stringValue);
                if (!string.IsNullOrEmpty(fromGuid) && fromGuid != path)
                {
                    pathProperty.stringValue = fromGuid;
                    path = fromGuid;
                }
            }
            return path;
        }

        static bool NeedsBuildWarning(SerializedProperty property, out string path)
        {
            path = property.FindPropertyRelative("path").stringValue;
            return !string.IsNullOrEmpty(path)
                   && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null
                   && !CinematicValidator.IsInBuildSettings(path);
        }

        internal static void AddToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != path)
                    continue;
                scenes[i] = new EditorBuildSettingsScene(path, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Cinematics] Escena añadida a la build: {path}");
        }
    }
}

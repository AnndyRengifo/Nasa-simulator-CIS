using UnityEditor;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>Desplegable con los siete identificadores oficiales (y opción de valor personalizado).</summary>
    [CustomPropertyDrawer(typeof(CinematicIdAttribute))]
    internal sealed class CinematicIdDrawer : PropertyDrawer
    {
        const string CustomOption = "Otro (personalizado)…";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (property.propertyType != SerializedPropertyType.String || IsKnownOrEmpty(property.stringValue))
                return line;
            return line * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var ids = CinematicIds.All;
            var options = new GUIContent[ids.Count + 2];
            options[0] = new GUIContent("(ninguno)");
            for (int i = 0; i < ids.Count; i++)
                options[i + 1] = new GUIContent(ids[i]);
            options[options.Length - 1] = new GUIContent(CustomOption);

            string value = property.stringValue;
            int selected = string.IsNullOrEmpty(value) ? 0 : options.Length - 1;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == value)
                    selected = i + 1;
            }

            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            int choice = EditorGUI.Popup(line, label, selected, options);
            if (EditorGUI.EndChangeCheck() && choice != selected)
            {
                if (choice == 0)
                    property.stringValue = string.Empty;
                else if (choice <= ids.Count)
                    property.stringValue = ids[choice - 1];
                else if (IsKnownOrEmpty(property.stringValue))
                    property.stringValue = "custom_id";
            }

            if (!IsKnownOrEmpty(property.stringValue))
            {
                var second = new Rect(position.x, line.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
                property.stringValue = EditorGUI.TextField(second, " ", property.stringValue);
            }
            EditorGUI.EndProperty();
        }

        static bool IsKnownOrEmpty(string value) => string.IsNullOrEmpty(value) || CinematicIds.IsKnown(value);
    }
}

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// วาดช่อง [SceneName] เป็น Dropdown รายชื่อ Scene ใน Build Settings (File > Build Profiles)
/// ค่าที่ไม่อยู่ใน Build Settings จะขึ้นเป็นสีแดง "(not in build)" เพราะเกมโหลด Scene นั้นไม่ได้
/// </summary>
[CustomPropertyDrawer(typeof(SceneNameAttribute))]
public class SceneNameDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        List<string> names = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                names.Add(Path.GetFileNameWithoutExtension(scene.path));
        }

        string current = property.stringValue;
        int index = names.IndexOf(current);

        List<string> options = new List<string>(names);
        if (index < 0)
        {
            // ค่าปัจจุบันไม่อยู่ใน Build Settings: โชว์ไว้บนสุดให้เห็นว่าผิด
            options.Insert(0, string.IsNullOrEmpty(current) ? "(none)" : current + "  (not in build)");
            index = 0;
        }

        Color previous = GUI.color;
        if (names.IndexOf(current) < 0)
            GUI.color = new Color(1f, 0.55f, 0.55f);

        EditorGUI.BeginProperty(position, label, property);
        int picked = EditorGUI.Popup(position, label.text, index, options.ToArray());
        EditorGUI.EndProperty();

        GUI.color = previous;

        string pickedName = options[picked];
        if (names.Contains(pickedName) && pickedName != current)
            property.stringValue = pickedName;
    }
}
#endif

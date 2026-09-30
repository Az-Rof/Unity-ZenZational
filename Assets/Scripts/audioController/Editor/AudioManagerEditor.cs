using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(AudioManager))]
public class AudioManagerEditor : Editor
{
    SerializedProperty musicSounds;
    SerializedProperty sfxSounds;
    bool musicOpen = true;
    bool sfxOpen = true;
    HashSet<string> expanded = new HashSet<string>();

    void OnEnable()
    {
        musicSounds = serializedObject.FindProperty("musicSounds");
        sfxSounds = serializedObject.FindProperty("sfxSounds");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        musicOpen = DrawSoundArray(musicSounds, "Music Sounds", musicOpen);
        sfxOpen = DrawSoundArray(sfxSounds, "SFX Sounds", sfxOpen);
        DrawPropertiesExcluding(serializedObject, "musicSounds", "sfxSounds");
        serializedObject.ApplyModifiedProperties();
    }

    bool DrawSoundArray(SerializedProperty arrayProp, string title, bool open)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        open = EditorGUILayout.Foldout(open, title, true);

        int deleteIndex = -1;
        if (open)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = element.FindPropertyRelative("name");
                SerializedProperty clipProp = element.FindPropertyRelative("audioClip");
                string path = element.propertyPath;

                string header = !string.IsNullOrEmpty(nameProp.stringValue)
                    ? nameProp.stringValue
                    : (clipProp.objectReferenceValue != null ? clipProp.objectReferenceValue.name : "Audio " + i);

                bool elemOpen = expanded.Contains(path);
                EditorGUILayout.BeginHorizontal();
                elemOpen = EditorGUILayout.Foldout(elemOpen, header, true);
                if (GUILayout.Button("-", GUILayout.Width(20)) && arrayProp.arraySize > 1)
                    deleteIndex = i;
                EditorGUILayout.EndHorizontal();

                if (elemOpen) expanded.Add(path);
                else expanded.Remove(path);

                if (elemOpen)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(nameProp);
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(clipProp);
                    if (EditorGUI.EndChangeCheck() && clipProp.objectReferenceValue != null)
                    {
                        nameProp.stringValue = clipProp.objectReferenceValue.name;
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        if (GUILayout.Button("+ Add " + title))
            arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);

        if (deleteIndex >= 0)
            arrayProp.DeleteArrayElementAtIndex(deleteIndex);

        return open;
    }
}
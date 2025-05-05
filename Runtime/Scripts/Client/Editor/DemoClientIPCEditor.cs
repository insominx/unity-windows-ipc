using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MergePlotProxy))]
public class DemoClientIPCEditor : Editor
{
    SerializedProperty _processProp;

    //-----------------------------------------------------------------------------
    void OnEnable()
    {
        _processProp = serializedObject.FindProperty("processToLaunch");
    }

    //-----------------------------------------------------------------------------
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "processToLaunch");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("HM Proxy Executable", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(_processProp, new GUIContent("Process To Launch"));

        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("Select Executable", "", "exe");
            if (!string.IsNullOrEmpty(path))
            {
                _processProp.stringValue = path;
                // Prevent layout-mismatch errors caused by the native dialog
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

}

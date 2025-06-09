using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ClientServerIpcBridge))]
public class ClientServerIpcBridgeEditor : Editor
{
    SerializedProperty _processProp;
    SerializedProperty _launchProp;

    //-----------------------------------------------------------------------------
    void OnEnable()
    {
        _processProp = serializedObject.FindProperty("processToLaunch");
        _launchProp = serializedObject.FindProperty("launchProcess");
    }

    //-----------------------------------------------------------------------------
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "processToLaunch", "launchProcess");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Example Serer Executable", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_launchProp, new GUIContent("Launch On Start"));
        EditorGUILayout.BeginHorizontal();
        _processProp.stringValue = EditorGUILayout.TextField("Process To Launch", _processProp.stringValue);

        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("Select Executable", "", "exe");
            if (!string.IsNullOrEmpty(path))
            {
                _processProp.stringValue = path;
                // Prevent layout-mismatch errors caused by the native dialog
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.EndHorizontal();
        serializedObject.ApplyModifiedProperties();
    }
}

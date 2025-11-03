using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(NetworkManager))]
public class NetworkManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        NetworkManager networkManager = (NetworkManager)target;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Buttons are only allowed at runtime.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Net controls", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Start Host"))
        {
            networkManager.StartHost();
        }

        if (GUILayout.Button("Start Server"))
        {
            networkManager.StartServer();
        }

        if (GUILayout.Button("Start Client"))
        {
            networkManager.StartClient();
        }

        EditorGUILayout.EndHorizontal();
    }
}
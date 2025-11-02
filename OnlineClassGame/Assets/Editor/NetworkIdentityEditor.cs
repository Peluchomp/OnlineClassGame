using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class NetworkIdentityEditor
{
    static NetworkIdentityEditor()
    {
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        bool sceneModified = false;

        NetworkIdentity[] identities = GameObject.FindObjectsOfType<NetworkIdentity>();

        foreach (var identity in identities)
        {
            if (identity.gameObject.scene == scene)
            {
                if (string.IsNullOrEmpty(identity.sceneId))
                {
                    SerializedObject so = new SerializedObject(identity);

                    SerializedProperty idProperty = so.FindProperty("m_sceneId");

                    idProperty.stringValue = System.Guid.NewGuid().ToString();

                    so.ApplyModifiedProperties();

                    sceneModified = true;
                }
            }
        }

        if (sceneModified)
        {
            Debug.Log("NetworkIdentity: Assigned new Scene IDs.");
        }
    }

    [MenuItem("Tools/Netcode/Generate Scene IDs")]
    private static void GenerateAllSceneIds()
    {
        NetworkIdentity[] identities = GameObject.FindObjectsOfType<NetworkIdentity>();
        int count = 0;

        foreach (var identity in identities)
        {
            if (string.IsNullOrEmpty(identity.sceneId))
            {
                SerializedObject so = new SerializedObject(identity);
                SerializedProperty idProperty = so.FindProperty("m_sceneId");
                idProperty.stringValue = System.Guid.NewGuid().ToString();
                so.ApplyModifiedProperties();
                count++;
            }
        }

        Debug.Log($"NetworkIdentity: Manually generated {count} new Scene IDs.");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
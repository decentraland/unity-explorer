using UnityEngine;
using UnityEditor;
using System.Collections;
using UnityEngine.Networking;
using System;
using Unity.EditorCoroutines.Editor;

[Serializable]
public class Folder
{
    public string path;
}

[Serializable]
public class DCLWorkspace
{
    public Folder[] folders;
}

[Serializable]
public class Scene
{
    public string[] parcels;
}

[Serializable]
public class SceneInfo
{
    public Scene scene;
}

public static class SceneFetcherEditor
{
    private const string baseUrl = "https://raw.githubusercontent.com/decentraland/sdk7-goerli-plaza/main/";

    [MenuItem("Custom Tools/Fetch Scene Coordinates")]
    private static void FetchSceneCoordinates()
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(FetchDCLWorkspace());
    }

    private static IEnumerator FetchDCLWorkspace()
    {
        string workspaceUrl = baseUrl + "dcl-workspace.json";
        using (UnityWebRequest webRequest = UnityWebRequest.Get(workspaceUrl))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error: " + webRequest.error);
            }
            else
            {
                string json = webRequest.downloadHandler.text;
                DCLWorkspace workspace = JsonUtility.FromJson<DCLWorkspace>(json);
                foreach (var folder in workspace.folders)
                {
                    yield return EditorCoroutineUtility.StartCoroutineOwnerless(FetchSceneCoordinates(folder.path));
                }
            }
        }
    }

    private static IEnumerator FetchSceneCoordinates(string scenePath)
    {
        string sceneUrl = baseUrl + scenePath + "/scene.json";
        using (UnityWebRequest webRequest = UnityWebRequest.Get(sceneUrl))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error fetching scene {scenePath}: {webRequest.error}");
            }
            else
            {
                string json = webRequest.downloadHandler.text;
                SceneInfo sceneInfo = JsonUtility.FromJson<SceneInfo>(json);
                Debug.Log($"Scene: {scenePath}");
                foreach (var parcel in sceneInfo.scene.parcels)
                {
                    Debug.Log($"Parcel: {parcel}");
                }
            }
        }
    }
}

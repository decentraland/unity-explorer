using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace DCL.Editor
{
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

    public class SceneFetcherEditor : EditorWindow
    {
        private const string BASE_URL = "https://raw.githubusercontent.com/decentraland/sdk7-goerli-plaza/main/";
        private static readonly List<string> FETCHED_PARCELS = new ();

        private TextField xInput;
        private TextField yInput;
        private Label resultLabel;
        private Button button;

        [MenuItem("Decentraland/Check SDK Scene Parcel Availability")]
        public static void ShowExample()
        {
            SceneFetcherEditor wnd = GetWindow<SceneFetcherEditor>();
            wnd.titleContent = new GUIContent("SDK Scene Parcel Checker");
            // EditorCoroutineUtility.StartCoroutineOwnerless(FetchDCLWorkspace());
        }

        private void OnEnable()
        {
            // Create UI
            var container = new VisualElement();
            rootVisualElement.Add(container);

            xInput = new TextField { label = "X:" };
            yInput = new TextField { label = "Y:" };
            container.Add(xInput);
            container.Add(yInput);

            button = new Button(CheckParcel) { text = "Check Parcel" };
            container.Add(button);

            resultLabel = new Label();
            rootVisualElement.Add(resultLabel);
        }

        private static async UniTask FetchDCLWorkspace()
        {
            const string WORKSPACE_URL = BASE_URL + "dcl-workspace.json";
            using var webRequest = UnityWebRequest.Get(WORKSPACE_URL);
            await webRequest.SendWebRequest().ToUniTask();

            if (webRequest.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                Debug.LogError("Error: " + webRequest.error);
            else
            {
                string json = webRequest.downloadHandler.text;
                DCLWorkspace workspace = JsonUtility.FromJson<DCLWorkspace>(json);

                foreach (Folder folder in workspace.folders)
                    await FetchSceneCoordinates(folder.path);
            }
        }

        private static async UniTask FetchSceneCoordinates(string scenePath)
        {
            string sceneUrl = BASE_URL + scenePath + "/scene.json";
            using var webRequest = UnityWebRequest.Get(sceneUrl);
            await webRequest.SendWebRequest().ToUniTask();

            if (webRequest.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                Debug.LogError($"Error fetching scene {scenePath}: {webRequest.error}");
            else
            {
                string json = webRequest.downloadHandler.text;
                SceneInfo sceneInfo = JsonUtility.FromJson<SceneInfo>(json);

                foreach (string parcel in sceneInfo.scene.parcels)
                    FETCHED_PARCELS.Add(parcel);
            }
        }

        private async void CheckParcel()
        {
            var userCoordinate = $"{xInput.value.Trim()},{yInput.value.Trim()}";

            if (FETCHED_PARCELS.Count == 0)
            {
                button.SetEnabled(false);
                resultLabel.text = "Fetching parcels...";

                await FetchDCLWorkspace();
                button.SetEnabled(true);
            }

            resultLabel.text = FETCHED_PARCELS.Contains(userCoordinate)
                ? $"Parcel {userCoordinate} is occupied."
                : $"Parcel {userCoordinate} is available.";
        }
    }
}

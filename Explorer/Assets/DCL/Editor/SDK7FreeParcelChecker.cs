using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
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

    public class SDK7FreeParcelChecker : EditorWindow
    {
        private const string BASE_URL = "https://raw.githubusercontent.com/decentraland/sdk7-goerli-plaza/main/";
        private static readonly Dictionary<string, string> FETCHED_PARCELS = new ();

        private TextField xInput;
        private TextField yInput;
        private Label resultLabel;
        private Button button;

        private void OnEnable()
        {
            var container = new VisualElement();
            rootVisualElement.Add(container);

            var note1 = new Label
            {
                text = "Please, publish test scenes in the area <b>[52,-52] - [71,-71]</b> parcels. This plaza is meant for testing (in contrast to other examples scenes)",
                enableRichText = true,
                style =
                {
                    color = new StyleColor(Color.yellow),
                    whiteSpace = WhiteSpace.Normal,
                },
            };
            rootVisualElement.Add(note1);

            var note2 = new Label
            {
                text = "<b>Note:</b> : you can also run <code>npm update-parcels</code> on the sdk7 repo root to validate that all scenes have unique parcels. It will tell you if there are any overlaps and which scenes & coords",
                enableRichText = true,
                style =
                {
                    backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f)),
                    whiteSpace = WhiteSpace.Normal,
                },
            };
            rootVisualElement.Add(note2);

            xInput = new TextField { label = "X:" };
            yInput = new TextField { label = "Y:" };
            container.Add(xInput);
            container.Add(yInput);

            button = new Button(CheckParcel) { text = "Check Parcel" };
            container.Add(button);

            resultLabel = new Label();
            rootVisualElement.Add(resultLabel);
        }

        [MenuItem("Decentraland/SDK/[Goerli] Check Parcel Availability")]
        public static void ShowExample()
        {
            SDK7FreeParcelChecker wnd = GetWindow<SDK7FreeParcelChecker>();
            wnd.titleContent = new GUIContent("Free Parcel Checker");
        }

        private static async UniTask FetchDCLWorkspace()
        {
            const string WORKSPACE_URL = BASE_URL + "dcl-workspace.json";
            DCLWorkspace workspace = await FetchJsonData<DCLWorkspace>(WORKSPACE_URL);

            if (workspace != null)
            {
                Debug.Log($"Occupied parcels for {workspace.folders}:");
                foreach (Folder folder in workspace.folders)
                    await FetchSceneCoordinates(folder.path);
            }
        }

        private static async UniTask FetchSceneCoordinates(string scenePath)
        {
            string sceneUrl = BASE_URL + scenePath + "/scene.json";
            SceneInfo sceneInfo = await FetchJsonData<SceneInfo>(sceneUrl);

            if (sceneInfo != null)
                foreach (string parcel in sceneInfo.scene.parcels)
                {
                    Debug.Log(parcel);
                    FETCHED_PARCELS[parcel] = scenePath;
                }
        }

        private static async UniTask<T> FetchJsonData<T>(string url)
        {
            Debug.Log(url);
            using var webRequest = UnityWebRequest.Get(url);
            await webRequest.SendWebRequest().ToUniTask();

            if (webRequest.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error fetching data from {url}: {webRequest.error}");
                return default(T);
            }

            string json = webRequest.downloadHandler.text;
            return JsonUtility.FromJson<T>(json);
        }

        private async void CheckParcel()
        {
            if (int.TryParse(xInput.value, out int x) == false || int.TryParse(yInput.value, out int y) == false)
            {
                resultLabel.text = "Invalid coordinates.";
                resultLabel.style.color = new StyleColor(Color.red);
                return;
            }

            var userCoordinate = $"{x},{y}";

            if (FETCHED_PARCELS.Count == 0)
            {
                button.SetEnabled(false);
                resultLabel.text = "Fetching parcels...";
                resultLabel.style.color = new StyleColor(Color.white);

                await FetchDCLWorkspace();
                button.SetEnabled(true);
            }

            string message;
            if (FETCHED_PARCELS.Keys.ToList().Contains(userCoordinate))
            {
                message = $"Parcel {userCoordinate} is occupied by {FETCHED_PARCELS[userCoordinate]}.";
                resultLabel.style.color = new StyleColor(Color.red);
            }
            else
            {
                message = $"Parcel {userCoordinate} is available.";
                resultLabel.style.color = new StyleColor(Color.green);
            }

            Debug.Log(message);
            resultLabel.text = message;
        }
    }
}

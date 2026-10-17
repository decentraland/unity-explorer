using Cysharp.Threading.Tasks;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace DCL.Editor
{
    public class GoerliGetSceneData : EditorWindow
    {
        private const string ABOUT_URL = "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main-latest/about";
        private const string SCENE_DATA_URL = "https://sdk-team-cdn.decentraland.org/ipfs";

        private TextField coordinatesInput;
        private Button getSceneDataButton;
        private RadioButton prettyPrintInput;

        private void OnEnable()
        {
            var container = new VisualElement();
            rootVisualElement.Add(container);

            var note1 = new Label
            {
                text = "This tool logs the scene json definition into the console from the <b>sdk-goerli-plaza</b> realm",
                enableRichText = true,
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                },
            };
            container.Add(note1);

            var note2 = new Label
            {
                text = "The coords format must be entered as: <b>X,Y</b>. For example: 78,-1",
                enableRichText = true,
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                },
            };
            container.Add(note2);

            prettyPrintInput = new RadioButton("Pretty print");
            container.Add(prettyPrintInput);

            coordinatesInput = new TextField { label = "X,Y:" };
            container.Add(coordinatesInput);

            getSceneDataButton = new Button(LogSceneData) { text = "Log Scene Data" };
            container.Add(getSceneDataButton);
        }

        [MenuItem("Decentraland/SDK/[Goerli] Get Scene Info")]
        public static void GetSceneInfoFromMenuItem()
        {
            GoerliGetSceneData wnd = GetWindow<GoerliGetSceneData>();
            wnd.titleContent = new GUIContent("Get Scene Info");
        }

        private void LogSceneData()
        {
            string coords = coordinatesInput.value;

            if (string.IsNullOrEmpty(coords))
            {
                Debug.LogError("Coordinates missing!");
                return;
            }

            Debug.Log($"Looking for scene data ({coords})..");
            LogSceneDataAsync().Forget();
            return;

            async UniTaskVoid LogSceneDataAsync()
            {
                try
                {
                    AboutResponseDto about = await FetchRealmAboutAsync();

                    SceneInfoDto scene = default;

                    foreach (string entry in about.configurations.scenesUrn)
                    {
                        string sceneHash = ExtractSceneHash(entry);

                        scene = await FetchSceneDataAsync(sceneHash);

                        if (scene.metadata.scene.@base == coords)
                            break;

                        foreach (string parcel in scene.metadata.scene.parcels)
                            if (parcel == coords)
                                break;
                    }

                    Debug.Log(JsonUtility.ToJson(scene, prettyPrintInput.value));
                }
                catch (UnityWebRequestException e)
                {
                    Debug.LogError($"Web request error: {e.UnityWebRequest.url} - {e.Message}");
                }
            }
        }

        private string ExtractSceneHash(string str)
        {
            const string PREFIX = "urn:decentraland:entity:";
            string withoutPrefix = str[PREFIX.Length..];
            int lastPartStartIndex = withoutPrefix.IndexOf('?');
            return lastPartStartIndex != -1 ? withoutPrefix[..lastPartStartIndex] : withoutPrefix;
        }

        private async UniTask<SceneInfoDto> FetchSceneDataAsync(string hash)
        {
            var request = UnityWebRequest.Get($"{SCENE_DATA_URL}/{hash}");
            await request.SendWebRequest().ToUniTask();

            if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                throw new UnityWebRequestException(request);

            string json = request.downloadHandler.text;
            SceneInfoDto sceneInfo = JsonUtility.FromJson<SceneInfoDto>(json);
            // Hash is not available in the json, so manually inject it
            sceneInfo.hash = hash;
            return sceneInfo;
        }

        private async UniTask<AboutResponseDto> FetchRealmAboutAsync()
        {
            var request = UnityWebRequest.Get(ABOUT_URL);
            await request.SendWebRequest().ToUniTask();

            if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                throw new UnityWebRequestException(request);

            string json = request.downloadHandler.text;
            return JsonUtility.FromJson<AboutResponseDto>(json);
        }

        // TODO: add more information if needed
        [Serializable]
        private struct SceneInfoDto
        {
            public string hash;
            public Metadata metadata;
            public string[] pointers;
            public string type;
            public long timestamp;
            public Content[] content;

            [Serializable]
            public struct Metadata
            {
                public Scene scene;
                public string name;
                public bool ecs7;
                public string runtimeVersion;
                public string owner;
                public string[] requiredPermissions;
                public string main;
                public string[] tags;

                [Serializable]
                public struct Scene
                {
                    public string @base;
                    public string[] parcels;
                }
            }

            [Serializable]
            public struct Content
            {
                public string file;
                public string hash;
            }
        }

        [Serializable]
        private struct AboutResponseDto
        {
            public Configurations configurations;

            [Serializable]
            public struct Configurations
            {
                public string[] scenesUrn;
            }
        }
    }
}

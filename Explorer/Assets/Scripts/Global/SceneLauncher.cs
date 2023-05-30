using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global
{
    [Serializable]
    public class SceneLauncher
    {
        [SerializeField] private UIDocument ui;
        [SerializeField] private List<string> scenes;

        private ISceneFacade currentScene;

        public void Initialize(SceneSharedContainer sceneSharedContainer, CancellationToken destroyCancellationToken)
        {
            DropdownField dropdown = ui.rootVisualElement.Q<DropdownField>("ScenesDropdown");

            dropdown.choices = scenes;

            IntegerField fps = ui.rootVisualElement.Q<IntegerField>("FPS");

            void OnSceneSelected(ChangeEvent<string> evt)
            {
                async UniTaskVoid LaunchScene()
                {
                    string directory = evt.newValue;
                    if (dropdown.choices.IndexOf(directory) < 0) return;

                    currentScene = await sceneSharedContainer.SceneFactory.CreateSceneFromStreamableDirectory(directory, destroyCancellationToken);
                    await currentScene.StartUpdateLoop(fps.value, destroyCancellationToken);
                }

                currentScene?.DisposeAsync().Forget();
                currentScene = null;

                LaunchScene().Forget();
            }

            dropdown.RegisterValueChangedCallback(OnSceneSelected);

            void OnFPSChanged(ChangeEvent<int> evt)
            {
                currentScene?.SetTargetFPS(Mathf.Clamp(evt.newValue, 0, 300));
            }

            fps.RegisterValueChangedCallback(OnFPSChanged);

            Button stopBtn = ui.rootVisualElement.Q<Button>("StopButton");
            stopBtn.clicked += OnStopButtonClicked;

            void OnStopButtonClicked()
            {
                dropdown.index = -1;
            }

            destroyCancellationToken.RegisterWithoutCaptureExecutionContext(() =>
            {
                currentScene?.DisposeAsync().Forget();
                dropdown.UnregisterValueChangedCallback(OnSceneSelected);
                fps.UnregisterValueChangedCallback(OnFPSChanged);
                stopBtn.clicked -= OnStopButtonClicked;
                currentScene = null;
            });
        }
    }
}

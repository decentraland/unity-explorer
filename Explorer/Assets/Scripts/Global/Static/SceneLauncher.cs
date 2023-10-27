using Cysharp.Threading.Tasks;
using ECS.Prioritization.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Static
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

            dropdown.RegisterValueChangedCallback(OnSceneSelected);

            fps.RegisterValueChangedCallback(OnFPSChanged);

            Button stopBtn = ui.rootVisualElement.Q<Button>("StopButton");
            stopBtn.clicked += OnStopButtonClicked;

            destroyCancellationToken.RegisterWithoutCaptureExecutionContext(() =>
            {
                currentScene?.DisposeAsync().Forget();
                dropdown.UnregisterValueChangedCallback(OnSceneSelected);
                fps.UnregisterValueChangedCallback(OnFPSChanged);
                stopBtn.clicked -= OnStopButtonClicked;
                currentScene = null;
            });

            return;

            void OnStopButtonClicked()
            {
                dropdown.index = -1;
            }

            void OnSceneSelected(ChangeEvent<string> evt)
            {
                currentScene?.DisposeAsync().Forget();
                currentScene = null;

                LaunchSceneAsync().Forget();
                return;

                async UniTaskVoid LaunchSceneAsync()
                {
                    string directory = evt.newValue;
                    if (dropdown.choices.IndexOf(directory) < 0) return;

                    currentScene = await sceneSharedContainer.SceneFactory.CreateSceneFromStreamableDirectoryAsync(directory, new PartitionComponent(), destroyCancellationToken);
                    await currentScene.StartUpdateLoopAsync(fps.value, destroyCancellationToken);
                }
            }

            void OnFPSChanged(ChangeEvent<int> evt)
            {
                currentScene?.SetTargetFPS(Mathf.Clamp(evt.newValue, 0, 300));
            }
        }
    }
}

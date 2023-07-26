using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ECS.Profiling
{
    public class ProfilerView : MonoBehaviour, IProfilerView
    {
        [SerializeField]
        private GameObject debugViewWindow;

        [SerializeField]
        private TMP_Text averageFrameRate;

        [SerializeField]
        private TMP_Text hiccupCounter;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        public event Action OnOpen;
        public event Action OnClose;

        private void Start()
        {
            openButton.onClick.AddListener(OpenProfilerWindow);
            closeButton.onClick.AddListener(CloseProfilerWindow);
        }

        private void CloseProfilerWindow()
        {
            openButton.gameObject.SetActive(true);
            debugViewWindow.gameObject.SetActive(false);
            OnClose?.Invoke();
        }

        private void OpenProfilerWindow()
        {
            openButton.gameObject.SetActive(false);
            debugViewWindow.gameObject.SetActive(true);
            OnOpen?.Invoke();
        }

        public void SetFPS(float averageFrameTimeInSeconds)
        {
            float frameTimeInMS = averageFrameTimeInSeconds * 1e3f;
            float frameRate = 1 / averageFrameTimeInSeconds;
            averageFrameRate.text = $"Frame Rate: {frameRate:F1} fps ({frameTimeInMS:F1} ms)";
        }

        public void SetHiccups(int hiccupCount)
        {
            hiccupCounter.text = $"Hiccups last 1000 frames: {hiccupCount}";
        }

        private void OnDestroy()
        {
            openButton.onClick.RemoveAllListeners();
            closeButton.onClick.RemoveAllListeners();
        }
    }
}

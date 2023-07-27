using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ECS.Profiling
{
    public class ProfilingView : MonoBehaviour, IProfilerView
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

        public bool IsOpen { get; private set; }

        private readonly string frameRateFormat = "Frame Rate: {0:1} fps ({1:1} ms)";
        private readonly string hiccupCounterFormat = "Hiccups last 1000 frames: {0}";


        private void Start()
        {
            openButton.onClick.AddListener(OpenProfilerWindow);
            closeButton.onClick.AddListener(CloseProfilerWindow);
        }
        public void SetFPS(float averageFrameTimeInSeconds)
        {
            float frameTimeInMS = averageFrameTimeInSeconds * 1e3f;
            float frameRate = 1 / averageFrameTimeInSeconds;

            averageFrameRate.SetText(frameRateFormat, frameRate, frameTimeInMS);
        }

        public void SetHiccups(int hiccupCount)
        {
            hiccupCounter.SetText(hiccupCounterFormat, hiccupCount);
        }
        private void CloseProfilerWindow()
        {
            openButton.gameObject.SetActive(true);
            debugViewWindow.gameObject.SetActive(false);
            IsOpen = false;
        }

        private void OpenProfilerWindow()
        {
            openButton.gameObject.SetActive(false);
            debugViewWindow.gameObject.SetActive(true);
            IsOpen = true;
        }

        private void OnDestroy()
        {
            openButton.onClick.RemoveAllListeners();
            closeButton.onClick.RemoveAllListeners();
        }
    }
}

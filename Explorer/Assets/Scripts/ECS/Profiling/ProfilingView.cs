using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ECS.Profiling
{
    public class ProfilingView : MonoBehaviour, IProfilerView
    {
        private readonly string frameRateFormat = "Frame Rate: {0:1} fps ({1:1} ms)";
        private readonly string hiccupCounterFormat = "Hiccups last 1000 frames: {0}";
        [SerializeField]
        private GameObject debugViewWindow;

        [SerializeField]
        private TMP_Text averageFrameRate;

        //Note: Doing it an input field so it can be selected and copied
        [SerializeField]
        private TMP_InputField version;

        [SerializeField]
        private TMP_Text hiccupCounter;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        public bool IsOpen { get; private set; }

        private void Start()
        {
            OpenProfilerWindow(); // Open on start

            openButton.onClick.AddListener(OpenProfilerWindow);
            closeButton.onClick.AddListener(CloseProfilerWindow);

            version.text = $"V: {Application.version}";
        }

        private void OnDestroy()
        {
            openButton.onClick.RemoveAllListeners();
            closeButton.onClick.RemoveAllListeners();
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
    }
}

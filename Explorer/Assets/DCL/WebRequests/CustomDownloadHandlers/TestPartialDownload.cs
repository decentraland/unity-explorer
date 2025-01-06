using UnityEngine;
using UnityEngine.UI;

namespace DCL.WebRequests.CustomDownloadHandlers
{
    public class TestPartialDownload : MonoBehaviour
    {
        private IPartialDownloadController downloadController;
        [SerializeField] private Button startButton;
        [SerializeField] private Button stopButton;

        private void Start()
        {
            downloadController = new PartialDownloadController("https://drive.usercontent.google.com/u/0/uc?id=1t2xIsh5y_V9cLxTGQVEl7d8S-_OaLrX5", "examplepartialdownload.png");
            downloadController.OnDownloadProgress += OnDownloadProgress;
            startButton.onClick.AddListener(StartDownload);
            stopButton.onClick.AddListener(StopDownload);
        }

        private void StartDownload()
        {
            downloadController.StartDownload();
        }

        private void StopDownload()
        {
            downloadController.StopDownload();
        }

        private void OnDownloadProgress(float obj)
        {
            Debug.Log("Download progress: " + (obj * 100));
        }

        private void OnCompleted()
        {
            Debug.Log("Download completed");
        }

        private void OnError(string obj)
        {
            Debug.LogError("Download error: " + obj);
        }
    }
}

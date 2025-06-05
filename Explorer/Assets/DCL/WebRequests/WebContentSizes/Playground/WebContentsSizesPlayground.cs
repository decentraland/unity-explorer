#nullable enable

using DCL.WebRequests.WebContentSizes.Sizes;
using System;
using UnityEngine;

namespace DCL.WebRequests.WebContentSizes.Playground
{
    public class WebContentsSizesPlayground : MonoBehaviour
    {
        [SerializeField] private MaxSize maxSize = new ();
        [SerializeField] private string targetUrl = string.Empty;

        private IWebContentSizes? webContentSizes;

        private void Start()
        {
            webContentSizes = AvailableWebContentSizes();
        }

        [ContextMenu(nameof(LaunchAsync))]
        public async void LaunchAsync()
        {
            if (webContentSizes is null)
            {
                print("No IWebContentSizes available: launch scene");
                return;
            }

            bool isOk = await webContentSizes.IsOkSizeAsync(new Uri(targetUrl), destroyCancellationToken);
            print($"Size for {targetUrl} is ok: {isOk}");
        }

        private IWebContentSizes AvailableWebContentSizes() =>
            webContentSizes ?? new IWebContentSizes.Default(maxSize, IWebRequestController.UNITY);
    }
}

using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunityRequestsReceivedGroupView : MonoBehaviour
    {
        public event Action<string>? CommunityButtonClicked;

        [SerializeField] public ImageView communityThumbnail = null!;
        [SerializeField] private TMP_Text communityTitle = null!;
        [SerializeField] private TMP_Text requestsReceivedText = null!;
        [SerializeField] private Button communityButton = null!;

        private string? currentCommunityId;

        private void Awake()
        {
            communityButton.onClick.AddListener(() =>
            {
                if (currentCommunityId != null)
                    CommunityButtonClicked?.Invoke(currentCommunityId);
            });
        }

        public void SetCommunityId(string id) =>
            currentCommunityId = id;

        public void SetTitle(string title) =>
            communityTitle.text = title;

        public void SetRequestsReceived(int requestsCount) =>
            requestsReceivedText.text = $"{requestsCount} Request{(requestsCount > 1 ? "s" : "")} Received";
    }
}

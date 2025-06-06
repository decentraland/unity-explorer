using DCL.UI;
using DCL.WebRequests;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class MyCommunityCardView : MonoBehaviour
    {
        public event Action<string> MainButtonClicked;

        [SerializeField] private TMP_Text communityTitle;
        [SerializeField] private GameObject userRoleContainer;
        [SerializeField] private TMP_Text userRole;
        [SerializeField] private ImageView communityThumbnail;
        [SerializeField] private GameObject communityLiveMark;
        [SerializeField] private Button mainButton;
        [SerializeField] private Sprite defaultThumbnailSprite;

        private ImageController imageController;
        private string currentCommunityId;

        private void Awake() =>
            mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(currentCommunityId));

        private void OnDestroy() =>
            mainButton.onClick.RemoveAllListeners();

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(communityThumbnail, webRequestController);
        }

        public void SetCommunityThumbnail(string imageUrl)
        {
            if (!string.IsNullOrEmpty(imageUrl))
                imageController?.RequestImage(imageUrl, hideImageWhileLoading: true);
            else
                imageController.SetImage(defaultThumbnailSprite);
        }

        public void SetCommunityId(string id) =>
            currentCommunityId = id;

        public void SetTitle(string title) =>
            communityTitle.text = title;

        public void SetUserRole(CommunityMemberRole role)
        {
            userRoleContainer.SetActive(role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            userRole.text = role.ToString();
        }

        public void SetLiveMarkAsActive(bool isLiveMark) =>
            communityLiveMark.SetActive(isLiveMark);
    }
}

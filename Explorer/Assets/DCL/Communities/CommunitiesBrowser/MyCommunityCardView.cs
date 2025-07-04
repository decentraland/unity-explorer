using DCL.UI;
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
        [field: SerializeField] public ImageView communityThumbnail;
        [SerializeField] private GameObject communityLiveMark;
        [SerializeField] private Button mainButton;

        private string currentCommunityId;

        private void Awake() =>
            mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(currentCommunityId));

        private void OnDestroy() =>
            mainButton.onClick.RemoveAllListeners();

        public void SetCommunityId(string id) =>
            currentCommunityId = id;

        public void SetTitle(string title) =>
            communityTitle.text = title;

        public void SetUserRole(CommunityMemberRole role)
        {
            userRoleContainer.SetActive(role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            var roleString = role.ToString();
            userRole.text = $"{char.ToUpperInvariant(roleString[0])}{roleString[1..]}";
        }

        public void SetLiveMarkAsActive(bool isLiveMark) =>
            communityLiveMark.SetActive(isLiveMark);
    }
}

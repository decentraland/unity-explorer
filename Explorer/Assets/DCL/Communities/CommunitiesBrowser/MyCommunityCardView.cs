using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class MyCommunityCardView : MonoBehaviour
    {
        public event Action<string>? MainButtonClicked;

        [SerializeField] private TMP_Text communityTitle = null!;
        [SerializeField] private GameObject userRoleContainer = null!;
        [SerializeField] private TMP_Text userRole = null!;
        [field: SerializeField] public ImageView communityThumbnail = null!;
        [SerializeField] private Button mainButton = null!;

        private string currentCommunityId;

        private void Awake() =>
            mainButton.onClick.AddListener(() =>
            {
                if (currentCommunityId != null)
                    MainButtonClicked?.Invoke(currentCommunityId);
            });

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
    }
}

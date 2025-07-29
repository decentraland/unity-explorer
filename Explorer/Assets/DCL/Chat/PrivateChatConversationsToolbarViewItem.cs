using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using UnityEngine;

namespace DCL.Chat
{
    public class PrivateChatConversationsToolbarViewItem : ChatConversationsToolbarViewItem
    {
        /// <summary>
        /// Provides the data required to display the profile picture.
        /// </summary>
        /// <param name="profileDataProvider">A way to access profile data.</param>
        /// <param name="userColor">The color of the user's profile picture. It affects the tooltip too.</param>
        /// <param name="faceSnapshotUrl">The URL to the profile picture.</param>
        public void SetProfileData(ProfileRepositoryWrapper profileDataProvider, Color userColor, string faceSnapshotUrl)
        {
            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);
            thumbnailView.GetComponent<ProfilePictureView>().Setup(profileDataProvider, userColor, faceSnapshotUrl);
            tooltipText.color = userColor;
        }

        protected override void Start()
        {
            base.Start();
            removeButton.gameObject.SetActive(true);
            connectionStatusIndicatorContainer.SetActive(true);
        }
    }
}

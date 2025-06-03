using DCL.UI.ProfileElements;
using MVC;
using UnityEngine;

namespace DCL.Chat
{
    public class PrivateChatConversationsToolbarViewItem : ChatConversationsToolbarViewItem
    {
        /// <summary>
        /// Provides the data required to display the profile picture.
        /// </summary>
        /// <param name="viewDependencies">A set of system tools for views.</param>
        /// <param name="userColor">The color of the user's profile picture. It affects the tooltip too.</param>
        /// <param name="faceSnapshotUrl">The URL to the profile picture.</param>
        /// <param name="userId">The Id of the user (wallet Id).</param>
        public void SetProfileData(ViewDependencies viewDependencies, Color userColor, string faceSnapshotUrl, string userId)
        {
            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);
            thumbnailView.GetComponent<ProfilePictureView>().SetupWithDependencies(viewDependencies, userColor, faceSnapshotUrl, userId);
            tooltipText.color = userColor;
        }

        protected override void Start()
        {
            base.Start();
            removeButton.gameObject.SetActive(true);
            connectionStatusIndicatorContainer.gameObject.SetActive(true);
        }
    }
}

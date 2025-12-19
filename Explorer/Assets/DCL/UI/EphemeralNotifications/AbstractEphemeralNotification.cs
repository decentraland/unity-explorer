using DCL.Profiles;
using DCL.UI.ProfileElements;
using TMPro;
using UnityEngine;

namespace DCL.UI.EphemeralNotifications
{
    /// <summary>
    ///     The visual representation of a notification in the ephemeral notifications panel.
    ///     Inherit from it to define a new type of notification and add the script to the prefab.
    /// </summary>
    public abstract class AbstractEphemeralNotification : MonoBehaviour
    {
        [SerializeField]
        protected CanvasGroup canvasGroup;

        [SerializeField]
        protected SimpleUserNameElement usernameElement;

        [SerializeField]
        protected TMP_Text label;

        /// <summary>
        ///     Builds the notification UI.
        /// </summary>
        /// <param name="sender">Profile data of the user that sent the notification.</param>
        /// <param name="textValues">The values used to compose the label of the notification.</param>
        public virtual void SetData(Profile sender, string[] textValues)
        {
            usernameElement.Setup(sender);
        }

        /// <summary>
        ///     Changes the opacity of the entire notification.
        /// </summary>
        /// <param name="newOpacity">The opacity of the notification, zero meaning transparent.</param>
        public void SetOpacity(float newOpacity)
        {
            canvasGroup.alpha = newOpacity;
        }
    }
}
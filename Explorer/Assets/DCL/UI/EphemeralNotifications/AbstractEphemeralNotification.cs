using DCL.Profiles;
using DCL.UI.ProfileElements;
using TMPro;
using UnityEngine;

namespace DCL.UI.EphemeralNotifications
{
    public  class AbstractEphemeralNotification : MonoBehaviour
    {
        [SerializeField]
        protected SimpleUserNameElement usernameElement;

        [SerializeField]
        protected TMP_Text label;

        public virtual void SetData(Profile sender, string[] textValues)
        {
            usernameElement.Setup(sender);
        }
    }
}

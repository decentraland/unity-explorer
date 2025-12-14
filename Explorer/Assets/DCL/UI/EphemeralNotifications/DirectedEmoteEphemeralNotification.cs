using DCL.Profiles;
using DCL.UI.EphemeralNotifications;
using UnityEngine;

namespace DCL.SocialEmotes.UI
{
    public class DirectedEmoteEphemeralNotification : AbstractEphemeralNotification
    {
        [SerializeField]
        private string textTemplate = "invited you to <b>{0}</b> with them";

        public override void SetData(Profile sender, string[] textValues)
        {
            base.SetData(sender, textValues);

            label.text = string.Format(textTemplate, textValues[0]);
        }
    }
}
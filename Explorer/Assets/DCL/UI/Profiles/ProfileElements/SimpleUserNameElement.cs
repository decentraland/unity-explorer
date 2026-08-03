using DCL.Profiles;
using TMPro;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    public class SimpleUserNameElement : MonoBehaviour
    {
        [field: SerializeField] private TMP_Text userNameText;
        [field: SerializeField] private TMP_Text userNameHashtagText;
        [field: SerializeField] private GameObject verifiedMark;

        public void Setup(Profile.CompactInfo profile)
        {
            // ValidatedName, not Name: the Name getter hands back exactly what the profile owner wrote, while its
            // setter derives ValidatedName from it by keeping only alphanumeric characters. DisplayName is that same
            // value with the #XXXX suffix already appended, which userNameHashtagText renders on its own below, so
            // using it here would print the suffix twice.
            // Keeping only alphanumerics leaves nothing at all of a name written entirely in emoji or punctuation,
            // so fall back to what the owner wrote rather than showing a blank label. Escaping is what makes that
            // safe to render.
            SetUserName(string.IsNullOrEmpty(profile.ValidatedName) ? profile.Name : profile.ValidatedName);
            userNameText.color = profile.UserNameColor;
            userNameHashtagText.gameObject.SetActive(!profile.HasClaimedName);

            if (!profile.HasClaimedName)
                userNameHashtagText.text = profile.WalletId;

            verifiedMark.SetActive(profile.HasClaimedName);
        }

        /// <summary>
        ///     The name belongs to another user, so it reaches the label escaped and capped: nothing it carries can
        ///     be read as a TMP tag, and its length cannot grow the layout without bound.
        /// </summary>
        private void SetUserName(string username) =>
            userNameText.text = RichTextSanitizer.EscapeAndTruncate(username, RichTextSanitizer.DEFAULT_NAME_LENGTH);
    }
}

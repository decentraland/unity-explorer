using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.BlockUserPrompt
{
    public class BlockUserPromptView : ViewBase, IView
    {
        private const string TITLE_BLOCK_FORMAT = "Are you sure you want to block {0}?";
        private const string TITLE_UNBLOCK_FORMAT = "Are you sure you want to unblock {0}?";

        [field: Header("References")]
        [field: SerializeField] public Button BackgroundCloseButton { get; set; }
        [field: SerializeField] public Button CancelButton { get; set; }
        [field: SerializeField] public Button BlockButton { get; set; }
        [field: SerializeField] public Button UnblockButton { get; set; }
        [field: SerializeField] public Image UnblockImage { get; set; }
        [field: SerializeField] public Image BlockImage { get; set; }
        [field: SerializeField] public GameObject BlockText { get; set; }
        [field: SerializeField] public GameObject UnblockText { get; set; }
        [field: SerializeField] public TMP_Text TitleText { get; set; }

        internal void SetTitle(BlockUserPromptParams.UserBlockAction blockAction, string userName)
        {
            string format = blockAction == BlockUserPromptParams.UserBlockAction.BLOCK ? TITLE_BLOCK_FORMAT : TITLE_UNBLOCK_FORMAT;
            TitleText.text = string.Format(format, userName);
        }

        internal void ConfigureButtons(BlockUserPromptParams.UserBlockAction blockAction)
        {
            bool isBlock = blockAction == BlockUserPromptParams.UserBlockAction.BLOCK;
            BlockButton.gameObject.SetActive(isBlock);
            BlockImage.gameObject.SetActive(isBlock);
            BlockText.gameObject.SetActive(isBlock);

            UnblockButton.gameObject.SetActive(!isBlock);
            UnblockImage.gameObject.SetActive(!isBlock);
            UnblockText.gameObject.SetActive(!isBlock);
        }
    }
}

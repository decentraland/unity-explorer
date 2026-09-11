using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class SelectAvatarForNewAccountAuthView : ViewBase
    {
        [field: SerializeField] public GameObject MaleAvatarPresetsRoot { get; private set; } = null!;
        [field: SerializeField] public GameObject FemaleAvatarPresetsRoot { get; private set; } = null!;
        [field: SerializeField] public Button[] MaleAvatarPresets { get; private set; } = null!;
        [field: SerializeField] public Button[] FemaleAvatarPresets { get; private set; } = null!;
        [field: SerializeField] public RectTransform SelectedSlotObj { get; private set; } = null!;
        [field: SerializeField] public Button BackButton { get; private set; } = null!;
        [field: SerializeField] public Button ContinueButton { get; private set; } = null!;

        [field: Header("Body Type Selector")]
        [field: SerializeField]
        public Button BodyTypeDropdownButton { get; private set; } = null!;
        [field: SerializeField]
        public GameObject BodyTypeDropdownPanel { get; private set; } = null!;
        [field: SerializeField]
        public Button BodyTypeOptionA { get; private set; } = null!;
        [field: SerializeField]
        public Button BodyTypeOptionB { get; private set; } = null!;
        [field: SerializeField]
        public TMPro.TMP_Text BodyTypeLabel { get; private set; } = null!;
        [field: SerializeField]
        public RectTransform ChevronIcon { get; private set; } = null!;
        [field: SerializeField]
        public GameObject DropdownManIcon { get; private set; } = null!;
        [field: SerializeField]
        public GameObject DropdownWomanIcon { get; private set; } = null!;
        [field: SerializeField]
        public GameObject CheckmarkIconA { get; private set; } = null!;
        [field: SerializeField]
        public GameObject CheckmarkIconB { get; private set; } = null!;

        public void Show() =>
            ShowAsync(CancellationToken.None).Forget();

        public void Hide() =>
            HideAsync(CancellationToken.None).Forget();

        /// <summary>
        ///     Moves the selection frame into the given slot and stretches it over the whole button.
        /// </summary>
        public void MoveSelectedSlotTo(Button slot)
        {
            SelectedSlotObj.SetParent(slot.transform, false);
            SelectedSlotObj.anchorMin = Vector2.zero;
            SelectedSlotObj.anchorMax = Vector2.one;
            SelectedSlotObj.offsetMin = Vector2.zero;
            SelectedSlotObj.offsetMax = Vector2.zero;
            SelectedSlotObj.localScale = Vector3.one;
            SelectedSlotObj.gameObject.SetActive(true);
        }

        public void SetBodyTypeDropdownOpen(bool isOpen)
        {
            BodyTypeDropdownPanel.SetActive(isOpen);
            ChevronIcon.localRotation = Quaternion.Euler(0, 0, isOpen ? 180f : 0f);
        }

        public void UpdateBodyTypeUi(bool isMale)
        {
            BodyTypeLabel.text = isMale ? "BODY TYPE A" : "BODY TYPE B";

            DropdownManIcon.SetActive(isMale);
            DropdownWomanIcon.SetActive(!isMale);

            CheckmarkIconA.SetActive(isMale);
            CheckmarkIconB.SetActive(!isMale);

            MaleAvatarPresetsRoot.SetActive(isMale);
            FemaleAvatarPresetsRoot.SetActive(!isMale);

            UpdateBodyTypeLabelAsync(isMale).Forget();
        }

        private async UniTaskVoid UpdateBodyTypeLabelAsync(bool isMale)
        {
            string key = isMale ? "BODY_TYPE_A" : "BODY_TYPE_B";

            try
            {
                var localized = new LocalizedString("Authentication", key);

                AsyncOperationHandle<string> handle = localized.GetLocalizedStringAsync();
                await handle;

                if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded
                                     && !string.IsNullOrEmpty(handle.Result))
                    BodyTypeLabel.text = handle.Result;
            }
            catch
            {
                // keep fallback already set in UpdateBodyTypeUi
            }
        }
    }
}

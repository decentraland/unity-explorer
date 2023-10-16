using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AvatarRendering.AvatarShape.DemoScripts.UI
{
    public class AvatarInstantiatorView : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField amountofAvatarsToInstantiate;

        public Button addRandomAvatarButton;

        public Button destroyAllAvatarsButton;

        public Button destroyRandomAmountAvatarsButton;

        public Button randomizeWearablesButton;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private TMP_Text avatarCount;

        [SerializeField]
        private TMP_Text avatarLimitWarning;

        [SerializeField]
        private GameObject debugViewWindow;

        private void Start()
        {
            OpenProfilerWindow(); // Open on start

            openButton.onClick.AddListener(OpenProfilerWindow);
            closeButton.onClick.AddListener(CloseProfilerWindow);
            avatarLimitWarning.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        public int GetAvatarsToInstantiate() =>
            int.Parse(amountofAvatarsToInstantiate.text);

        private void CloseProfilerWindow()
        {
            openButton.gameObject.SetActive(true);
            debugViewWindow.gameObject.SetActive(false);
        }

        private void OpenProfilerWindow()
        {
            openButton.gameObject.SetActive(false);
            debugViewWindow.gameObject.SetActive(true);
        }

        public void SetAvatarCount(int newCount)
        {
            avatarLimitWarning.gameObject.SetActive(false);
            avatarCount.text = $"({newCount.ToString()})";
        }

        public void ShowMaxNumberWarning(int maxNumber)
        {
            avatarLimitWarning.text = $"You cannot instantiate more than {maxNumber} avatars";
            avatarLimitWarning.gameObject.SetActive(true);
        }
    }
}

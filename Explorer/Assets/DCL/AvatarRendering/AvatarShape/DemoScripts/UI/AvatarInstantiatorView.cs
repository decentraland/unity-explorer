using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AvatarRendering.AvatarShape.DemoScripts.UI
{
    public class AvatarInstantiatorView : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField amountofAvatarsToInstantiate;
        [SerializeField]
        private Toggle doSkin;

        public Button addRandomAvatarButton;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private GameObject debugViewWindow;

        private void Start()
        {
            OpenProfilerWindow(); // Open on start

            openButton.onClick.AddListener(OpenProfilerWindow);
            closeButton.onClick.AddListener(CloseProfilerWindow);
        }

        public int GetAvatarsToInstantiate() =>
            int.Parse(amountofAvatarsToInstantiate.text);

        public bool GetDoSkin() =>
            doSkin.isOn;

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
    }
}

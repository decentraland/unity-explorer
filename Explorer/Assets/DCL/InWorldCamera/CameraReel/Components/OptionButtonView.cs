using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class OptionButtonView : MonoBehaviour
    {
        [SerializeField] internal GameObject contextMenu;
        [SerializeField] internal GameObject hoverHelper;
        [SerializeField] internal Button optionButton;

        [Header("Controls")]
        [SerializeField] internal Toggle setAsPublic;
        [SerializeField] internal Button shareOnX;
        [SerializeField] internal Button copyLink;
        [SerializeField] internal Button download;
        [SerializeField] internal Button delete;

        private void Awake() =>
            ResetState();

        internal void ResetState()
        {
            contextMenu.SetActive(false);
            hoverHelper.SetActive(false);
            this.transform.localScale = Vector3.one;
        }

    }
}

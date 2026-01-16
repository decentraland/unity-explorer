using DCL.CharacterPreview;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    [RequireComponent(typeof(Animator))]
    public class LobbyScreenSubView : MonoBehaviour
    {
        [field: SerializeField]
        public Animator FinalizeAnimator { get; private set; } = null!;

        [field: SerializeField]
        public Button JumpIntoWorldButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject Description { get; private set; } = null!;

        [field: SerializeField]
        public GameObject DiffAccountButton { get; private set; } = null!;

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; } = null!;

        [field: SerializeField]
        public LocalizeStringEvent ProfileNameLabel { get; private set; } = null!;

        private void Awake()
        {
            FinalizeAnimator = GetComponent<Animator>();

            JumpIntoWorldButton.gameObject.SetActive(true);
            DiffAccountButton.SetActive(true);
            ProfileNameLabel.gameObject.SetActive(true);
            Description.SetActive(true);
            CharacterPreviewView.gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            FinalizeAnimator.enabled = true;
        }

        private void OnDisable()
        {
            FinalizeAnimator.enabled = false;
        }
    }
}

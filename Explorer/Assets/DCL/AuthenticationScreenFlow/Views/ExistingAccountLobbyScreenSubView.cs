using DCL.CharacterPreview;
using DCL.UI;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow
{
    [RequireComponent(typeof(Animator))]
    public class ExistingAccountLobbyScreenSubView : MonoBehaviour
    {
        [field: SerializeField]
        public Animator FinalizeAnimator { get; private set; } = null!;

        [field: SerializeField]
        public Button JumpIntoWorldButton { get; private set; } = null!;

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; } = null!;

        [SerializeField] private LocalizeStringEvent title;
        [SerializeField] private GameObject description;
        [SerializeField] private GameObject diffAccountButton;

        private StringVariable? profileNameLabel;

        private void Awake()
        {
            FinalizeAnimator = GetComponent<Animator>();

            title.gameObject.SetActive(true); // title
            description.SetActive(true);

            JumpIntoWorldButton.gameObject.SetActive(true);
            diffAccountButton.SetActive(true);

            CharacterPreviewView.gameObject.SetActive(true);

            profileNameLabel = (StringVariable)title.StringReference["back_profileName"];
        }

        // private void OnEnable()
        // {
        //     FinalizeAnimator.enabled = true;
        // }
        //
        // private void OnDisable()
        // {
        //     FinalizeAnimator.enabled = false;
        // }

        public void ShowFor(string profileName)
        {
            profileNameLabel!.Value = profileName;
            JumpIntoWorldButton.interactable = true;

            // FinalizeAnimator.enabled = true;
            // FinalizeAnimator.ResetAnimator();
            FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);
        }

        public void FadeOut() =>
            FinalizeAnimator.SetTrigger(UIAnimationHashes.OUT);

        public void SlideBack() =>
            FinalizeAnimator.SetTrigger(UIAnimationHashes.BACK);
    }
}

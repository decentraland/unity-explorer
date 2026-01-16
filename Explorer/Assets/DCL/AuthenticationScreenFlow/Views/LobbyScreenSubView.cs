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
    public class LobbyScreenSubView : MonoBehaviour
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

        [Header("NEW USER")]
        [SerializeField] private GameObject newUserContainer;

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

        private void OnEnable()
        {
            FinalizeAnimator.enabled = true;
        }

        private void OnDisable()
        {
            FinalizeAnimator.enabled = false;
            newUserContainer.SetActive(false);
        }

        public void ShowExistingAccountLobby(string profileName)
        {
            profileNameLabel!.Value = profileName;

            JumpIntoWorldButton.gameObject.SetActive(true);
            JumpIntoWorldButton.transform.parent.gameObject.SetActive(true);
            JumpIntoWorldButton.interactable = true;

            title.gameObject.SetActive(true);
            description.SetActive(true);
            diffAccountButton.SetActive(true);

            newUserContainer.SetActive(false);

            FinalizeAnimator.enabled = true;
            FinalizeAnimator.ResetAnimator();
            FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);
        }

        public void ShowNewAccountLobby()
        {
            JumpIntoWorldButton.gameObject.SetActive(false);
            JumpIntoWorldButton.interactable = true;
            title.gameObject.SetActive(false);
            description.SetActive(false);
            diffAccountButton.SetActive(false);

            newUserContainer.SetActive(true);

            FinalizeAnimator.enabled = true;
            FinalizeAnimator.ResetAnimator();
            FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);
        }
    }
}

using DCL.UI;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.NftPrompt
{
    public class NftPromptView : ViewBase, IView
    {
        [field: SerializeField] public GameObject NftContent { get; private set; }
        [field: SerializeField] public GameObject MainErrorFeedbackContent { get; private set; }
        [field: SerializeField] public ImageView ImageNft { get; private set; }
        [field: SerializeField] public TextMeshProUGUI TextNftName { get; private set; }
        [field: SerializeField] public TextMeshProUGUI TextOwner { get; private set; }
        [field: SerializeField] public TextMeshProUGUI TextMultipleOwner { get; private set; }
        [field: SerializeField] public GameObject MultipleOwnersContainer { get; private set; }
        [field: SerializeField] public TextMeshProUGUI TextDescription { get; private set; }
        [field: SerializeField] public GameObject ContainerDescription { get; private set; }
        [field: SerializeField] public GameObject SpinnerGeneral { get; private set; }
        [field: SerializeField] public GameObject SpinnerNftImage { get; private set; }
        [field: SerializeField] public Button ButtonClose { get; private set; }
        [field: SerializeField] public Button ButtonCancel { get; private set; }
        [field: SerializeField] public Button ButtonOpenMarket { get; private set; }
        [field: SerializeField] public TextMeshProUGUI TextOpenMarketButton { get; private set; }
    }
}

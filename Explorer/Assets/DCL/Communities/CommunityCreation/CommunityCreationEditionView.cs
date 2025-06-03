using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionView : ViewBase, IView
    {

        [SerializeField] public Button backgroundCloseButton;
        [SerializeField] public Button cancelButton;
        [SerializeField] private GameObject getNamePanel;
        [SerializeField] private GameObject creationPanel;

        public void SetAsClaimedName(bool hasClaimedName)
        {
            getNamePanel.SetActive(!hasClaimedName);
            creationPanel.SetActive(hasClaimedName);
        }
    }
}

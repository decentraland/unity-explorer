using SuperScrollView;
using UnityEngine;
using DCL.UI.Utilities;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListView : MonoBehaviour
    {
        [field: SerializeField] public LoopListView2 LoopList { get; private set; }

        private void Awake()
        {
            LoopList.ScrollRect.SetScrollSensitivityBasedOnPlatform();
        }
    }
}

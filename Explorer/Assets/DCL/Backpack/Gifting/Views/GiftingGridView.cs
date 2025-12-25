using SuperScrollView;
using UnityEngine;

namespace DCL.Backpack.Gifting.Views
{
    public class GiftingGridView : MonoBehaviour
    {
        [field: SerializeField]
        public LoopGridView Grid { get; private set; }

        [field: SerializeField]
        public GameObject RegularResultsContainer { get; private set; }

        [field: SerializeField]
        public GameObject NoResultsContainer { get; private set; }
    }
}
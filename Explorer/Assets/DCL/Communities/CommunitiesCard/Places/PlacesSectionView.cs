using SuperScrollView;
using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlacesSectionView : MonoBehaviour
    {
        [field: SerializeField] private LoopGridView loopGrid { get; set; }

        public void SetActive(bool active) => gameObject.SetActive(active);
    }
}

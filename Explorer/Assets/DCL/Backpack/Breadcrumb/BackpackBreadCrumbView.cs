using UnityEngine;

namespace DCL.Backpack.Breadcrumb
{
    public class BackpackBreadCrumbView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject AllButtonArrow { get; private set; }

        [field: SerializeField]
        public NftSubCategoryFilterComponentView AllButton { get; private set; }

        [field: SerializeField]
        public NftSubCategoryFilterComponentView FilterButton { get; private set; }

        [field: SerializeField]
        public NftSubCategoryFilterComponentView SearchButton { get; private set; }
    }
}

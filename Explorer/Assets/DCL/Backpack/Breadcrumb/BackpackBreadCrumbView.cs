using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Breadcrumb
{
    public class BackpackBreadCrumbView : MonoBehaviour
    {
        [field: SerializeField]
        public NftSubCategoryFilterComponentView AllButton { get; private set; }

        [field: SerializeField]
        public NftSubCategoryFilterComponentView FilterButton { get; private set; }

        [field: SerializeField]
        public NftSubCategoryFilterComponentView SearchButton { get; private set; }
    }
}

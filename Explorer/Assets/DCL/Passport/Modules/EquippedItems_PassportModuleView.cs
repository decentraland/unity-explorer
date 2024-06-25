using UnityEngine;

namespace DCL.Passport.Modules
{
    public class EquippedItems_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public EquippedItem_PassportFieldView equippedItemPrefab;

        [field: SerializeField]
        public RectTransform EquippedItemsContainer { get; private set; }
    }
}

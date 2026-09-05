using DCL.Passport.Fields;
using UnityEngine;

namespace DCL.Passport.Modules
{
    public class EquippedItemsPassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public EquippedItemPassportFieldView equippedItemPrefab;

        [field: SerializeField]
        public RectTransform EquippedItemsContainer { get; private set; } = null!;
    }
}

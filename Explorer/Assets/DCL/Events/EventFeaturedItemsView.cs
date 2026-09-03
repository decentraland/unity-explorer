using DCL.Passport.Fields;
using TMPro;
using UnityEngine;

namespace DCL.Communities.EventInfo
{
    public class EventFeaturedItemsView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject Root { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text Title { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform ItemsContainer { get; private set; } = null!;

        [field: SerializeField]
        public EquippedItemPassportFieldView ItemPrefab { get; private set; } = null!;

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; } = null!;
    }
}

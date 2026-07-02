using DCL.Passport.Fields;
using UnityEngine;

namespace DCL.Passport.Modules.Creations
{
    public class CreationsDetails_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject MainContainer { get; private set; }

        [field: SerializeField]
        public GameObject MainLoadingSpinner { get; private set; }

        [field: SerializeField]
        public GameObject NoCreationsLabel { get; private set; }

        [field: SerializeField]
        public GameObject NoWearablesLabel { get; private set; }

        [field: SerializeField]
        public GameObject NoEmotesLabel { get; private set; }

        [field: SerializeField]
        public RectTransform CreatedWearablesContainer { get; private set; }

        [field: SerializeField]
        public RectTransform CreatedEmotesContainer { get; private set; }

        [field: SerializeField]
        public EquippedItem_PassportFieldView equippedItemPrefab { get; private set; }

    }
}

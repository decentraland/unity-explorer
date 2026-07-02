using DCL.Passport.Fields;
using UnityEngine;

namespace DCL.Passport.Modules.Creations
{
    public class CreationsDetailsPassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject MainLoadingSpinner { get; private set; } = null!;

        [field: SerializeField]
        public GameObject NoCreationsLabel { get; private set; } = null!;

        [field: SerializeField]
        public GameObject NoWearablesLabel { get; private set; } = null!;

        [field: SerializeField]
        public GameObject NoEmotesLabel { get; private set; } = null!;

        [field: SerializeField]
        public GameObject WearablesLabel { get; private set; } = null!;

        [field: SerializeField]
        public GameObject EmotesLabel { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform CreatedWearablesContainer { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform CreatedEmotesContainer { get; private set; } = null!;

        [field: SerializeField]
        public EquippedItem_PassportFieldView EquippedItemPrefab { get; private set; } = null!;

    }
}

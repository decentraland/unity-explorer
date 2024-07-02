using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Fields
{
    public class AdditionalField_PassportFieldView : MonoBehaviour
    {
        [field: SerializeField]
        public Image Logo { get; private set; }

        [field: SerializeField]
        public TMP_Text Title { get; private set; }

        [field: SerializeField]
        public TMP_Text Value { get; private set; }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class Link_PassportFieldView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform Container { get; private set; }

        [field: SerializeField]
        public TMP_Text Title { get; private set; }

        [field: SerializeField]
        public string Link { get; set; }

        [field: SerializeField]
        public Button LinkButton { get; private set; }
    }
}

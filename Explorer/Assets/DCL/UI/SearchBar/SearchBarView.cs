using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class SearchBarView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_InputField inputField;

        [field: SerializeField]
        public Button clearSearchButton;
    }
}

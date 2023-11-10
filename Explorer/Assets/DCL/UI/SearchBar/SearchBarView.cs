using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SearchBarView : MonoBehaviour
{
    [field: SerializeField]
    internal TMP_InputField inputField;

    [field: SerializeField]
    internal TMP_Text placeHolderText;

    [field: SerializeField]
    internal GameObject searchSpinner;

    [field: SerializeField]
    internal Button clearSearchButton;
}

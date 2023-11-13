using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FloatingPanelView : MonoBehaviour
{
    [field: SerializeField]
    public RectTransform rectTransform;

    [field: SerializeField]
    public Image placeImage;

    [field: SerializeField]
    public Button closeButton;

    [field: SerializeField]
    public Button jumpInButton;

    [field: SerializeField]
    public TMP_Text placeName;

    [field: SerializeField]
    public TMP_Text placeCreator;

    [field: SerializeField]
    public TMP_Text placeDescription;
}

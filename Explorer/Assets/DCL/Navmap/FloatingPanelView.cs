using System.Collections;
using System.Collections.Generic;
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
}

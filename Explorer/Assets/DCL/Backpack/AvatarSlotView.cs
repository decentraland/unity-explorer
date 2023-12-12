using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AvatarSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public event Action OnSlotButtonPressed;

    [field: SerializeField]
    public string Category { get; private set; }

    [field: SerializeField]
    public Button SlotButton { get; private set; }

    [field: SerializeField]
    public GameObject HoverTootlip { get; private set; }

    [field: SerializeField]
    public GameObject SelectedBackground { get; private set; }

    [field: SerializeField]
    public Button UnequipButton { get; private set; }

    [field: SerializeField]
    public TMP_Text CategoryText { get; private set; }

    public void Start()
    {
        CategoryText.text = Category;
        SlotButton.onClick.AddListener(() => OnSlotButtonPressed?.Invoke());
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        HoverTootlip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverTootlip.SetActive(false);
    }
}

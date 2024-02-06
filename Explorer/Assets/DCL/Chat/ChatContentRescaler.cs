using TMPro;
using UnityEngine;

public class ChatContentRescaler : MonoBehaviour
{
    public TMP_InputField inputField;
    public RectTransform inputFieldRectTransform;
    public RectTransform contentRectTransform;

    private Vector2 inputFieldRectTransformSize;
    private Vector2 contentRectTransformSize;
    private Vector2 resizedInputFieldRectTransformSize;
    private Vector2 resizedContentRectTransformSize;


    public void Start()
    {
        inputFieldRectTransformSize = inputFieldRectTransform.sizeDelta;
        contentRectTransformSize = contentRectTransform.sizeDelta;
        resizedInputFieldRectTransformSize = new Vector2(inputFieldRectTransformSize.x, inputFieldRectTransformSize.y);
        resizedContentRectTransformSize = new Vector2(contentRectTransformSize.x, contentRectTransformSize.y);
        inputField.onValueChanged.AddListener(OnInputValueChanged);
    }

    private void OnInputValueChanged(string value)
    {
        resizedInputFieldRectTransformSize.y = inputFieldRectTransformSize.y + inputField.preferredHeight;
        resizedContentRectTransformSize.y = contentRectTransformSize.y - inputField.preferredHeight;

        inputFieldRectTransform.sizeDelta = resizedInputFieldRectTransformSize;
        contentRectTransform.sizeDelta = resizedContentRectTransformSize;
    }
}

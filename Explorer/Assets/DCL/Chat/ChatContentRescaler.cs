using TMPro;
using UnityEngine;

public class ChatContentRescaler : MonoBehaviour
{
    //TODO: Fix this so the logic is properly distributed and no references to internal components of other game objects are made (like input field and inputFieldRectTransform)
    //Created ticket for this: #3247
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private RectTransform inputFieldRectTransform;
    [SerializeField] private RectTransform contentRectTransform;
    [SerializeField] private float minimumHeight;

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
        inputField.ForceLabelUpdate();

        if (inputField.preferredHeight < minimumHeight)
        {
            inputFieldRectTransform.sizeDelta = inputFieldRectTransformSize;
            contentRectTransform.sizeDelta = contentRectTransformSize;
            return;
        }

        resizedInputFieldRectTransformSize.y = inputField.preferredHeight;
        resizedContentRectTransformSize.y = contentRectTransformSize.y - inputField.preferredHeight + minimumHeight;

        inputFieldRectTransform.sizeDelta = resizedInputFieldRectTransformSize;
        contentRectTransform.sizeDelta = resizedContentRectTransformSize;
    }
}

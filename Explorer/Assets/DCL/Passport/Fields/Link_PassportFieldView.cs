using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Fields
{
    public class Link_PassportFieldView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform Container { get; private set; }

        [field: SerializeField]
        public TMP_Text Title { get; private set; }

        [field: SerializeField]
        public Button LinkButton { get; private set; }

        [field: SerializeField]
        public Button RemoveLinkButton { get; private set; }

        public string Id { get; set; }

        public string Url { get; set; }

        public bool IsInEditMode { get; private set; }

        public void SetAsEditable(bool isEditable)
        {
            RemoveLinkButton.gameObject.SetActive(isEditable);
            IsInEditMode = isEditable;
        }

        public void SetAsInteractable(bool isInteractable)
        {
            LinkButton.interactable = isInteractable;
            RemoveLinkButton.interactable = isInteractable;
        }
    }
}

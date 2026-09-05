using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ErrorPopup
{
    public class ErrorPopupView : ViewBase, IView
    {
        [SerializeField] private Image icon = null!;
        [SerializeField] private TMP_Text title = null!;
        [SerializeField] private TMP_Text description = null!;
        [SerializeField] private Button okButton = null!;

        [Header("Defaults")]
        [SerializeField] private string defaultTitle = "Error";
        [SerializeField] private string defaultDescription = "An unknown error occurred";
        [SerializeField] private Sprite defaultIcon = null!;

        public Button OkButton => okButton;

        public void Apply(ErrorPopupData data)
        {
            icon.Apply(data.Icon, defaultIcon);
            title.Apply(data.Title, defaultTitle);
            description.Apply(data.Description, defaultDescription);
        }
    }
}

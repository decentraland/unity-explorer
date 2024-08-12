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

        public Button OkButton => okButton;

        public void Apply(ErrorPopupData data)
        {
            if (data.Icon != null)
                icon.sprite = data.Icon;

            if (data.Title != null)
                title.text = data.Title;

            if (data.Description != null)
                description.text = data.Description;
        }
    }
}

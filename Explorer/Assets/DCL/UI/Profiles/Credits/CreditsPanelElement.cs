using System;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.UI.Credits
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="CreditsPanelView" />: the credits logo next to the amount, the whole of it clickable to get
    ///     more credits while top-up is enabled. It builds its own children, so it is complete wherever it is created; CreditsPanel.uss
    ///     styles them, so the document that hosts it imports that stylesheet.
    /// </summary>
    [UxmlElement]
    public partial class CreditsPanelElement : VisualElement, ICreditsPanelView
    {
        private const string USS_BLOCK = "credits-panel";
        private const string USS_TOP_UP = USS_BLOCK + "--top-up";
        private const string USS_ICON = USS_BLOCK + "__icon";
        private const string USS_AMOUNT = USS_BLOCK + "__amount";

        private const string ICON_NAME = "Icon";
        private const string AMOUNT_NAME = "Amount";

        private readonly Label amount;

        public bool IsShown
        {
            set => this.SetDisplayed(value);
        }

        public bool IsTopUpEnabled
        {
            get => ClassListContains(USS_TOP_UP);

            set
            {
                EnableInClassList(USS_TOP_UP, value);
                pickingMode = value ? PickingMode.Position : PickingMode.Ignore;
            }
        }

        [UxmlAttribute]
        public string Credits
        {
            get => amount.text;
            set => amount.text = value;
        }

        public Action? GetCreditsClicked { get; set; }

        public CreditsPanelElement()
        {
            AddToClassList(USS_BLOCK);

            var icon = new VisualElement { name = ICON_NAME, pickingMode = PickingMode.Ignore };
            icon.AddToClassList(USS_ICON);
            Add(icon);

            amount = new Label { name = AMOUNT_NAME, text = "0", pickingMode = PickingMode.Ignore };
            amount.AddToClassList(USS_AMOUNT);
            Add(amount);

            IsTopUpEnabled = true;
            this.AddManipulator(new Clickable(OnClicked));
        }

        private void OnClicked() =>
            GetCreditsClicked?.Invoke();
    }
}

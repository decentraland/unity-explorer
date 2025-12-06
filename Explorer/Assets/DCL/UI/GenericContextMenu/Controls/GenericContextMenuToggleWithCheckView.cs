using DCL.UI.Controls.Configs;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DCL.UI.Controls
{
    public class GenericContextMenuToggleWithCheckView : GenericContextMenuComponentBase
    {
        [SerializeField] protected Toggle toggleComponent;
        [SerializeField] protected TMP_Text textComponent;

        public override bool IsInteractable
        {
            get => toggleComponent.interactable;
            set => toggleComponent.interactable = value;
        }

        public override void UnregisterListeners() =>
            toggleComponent.onValueChanged.RemoveAllListeners();

        protected void RegisterListener(Action<bool> listener)
        {
            toggleComponent.onValueChanged.AddListener(new UnityAction<bool>(listener));
        }

        public void Configure(ToggleWithCheckContextMenuControlSettings settings, bool initialValue)
        {
            textComponent.SetText(settings!.toggleText);
            toggleComponent.group = settings.toggleGroup;
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.horizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.horizontalLayoutReverseArrangement;
            toggleComponent.SetIsOnWithoutNotify(initialValue);
            RegisterListener(settings.callback);
        }

        public override void RegisterCloseListener(Action listener)
        {
        }
    }
}

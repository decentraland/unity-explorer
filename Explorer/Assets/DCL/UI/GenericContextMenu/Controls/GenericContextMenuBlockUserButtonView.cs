using DCL.Profiles;
using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuBlockUserButtonView : GenericContextMenuComponentBase
    {
        [field: SerializeField] public Button ButtonComponent { get; private set; }

        public void Configure(BlockUserButtonContextMenuControlSettings settings)
        {
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.horizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.horizontalLayoutReverseArrangement;
            RegisterListener(settings.callback, settings.data);
        }

        public override void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();

        private void RegisterListener(Delegate listener, Profile data) =>
            ButtonComponent.onClick.AddListener(() => listener.DynamicInvoke(data));

        public override void RegisterCloseListener(Action listener) =>
            ButtonComponent.onClick.AddListener(new UnityAction(listener));
    }
}

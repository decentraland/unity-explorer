using DCL.Profiles;
using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuButtonWithProfileView : GenericContextMenuComponentBase
    {
        [field: SerializeField] public Button ButtonComponent { get; private set; }

        public void Configure(ButtonWithProfileContextMenuControlSettings settings)
        {
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.horizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.horizontalLayoutReverseArrangement;
            RegisterListener(settings.callback, settings.profile);
        }

        public override void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();

        private void RegisterListener(Action<Profile> listener, Profile profile) =>
            ButtonComponent.onClick.AddListener(() => listener.Invoke(profile));
        public override void RegisterCloseListener(Action listener)
        { }
    }
}

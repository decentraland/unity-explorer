using DCL.UI.Controls.Configs;
using System;
using TMPro;
using UnityEngine;

namespace DCL.UI.Controls
{
    public class GenericContextMenuTextView : GenericContextMenuComponentBase
    {
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }

        public void Configure(TextContextMenuControlSettings settings)
        {
            TextComponent.text = settings.Text;
        }

        public override void UnregisterListeners()
        {
        }

        public override void RegisterCloseListener(Action listener)
        {
        }
    }
}

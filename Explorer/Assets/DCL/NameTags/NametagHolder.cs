using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Nametags
{
    [RequireComponent(typeof(UIDocument))]
    public class NametagHolder : MonoBehaviour
    {
        private NametagElement? nametag;

        public NametagElement Nametag => nametag ?? throw new InvalidOperationException($"{nameof(NametagHolder)} is used before {nameof(OnEnable)} resolved its {nameof(NametagElement)}");

        // Visual flags live as CSS classes on the NametagElement and persist across pool reuse.
        // Reset transient state on release so a freshly-acquired holder cannot inherit a previous owner's voice chat badge or chat bubble.
        public void ResetTransientVisualState()
        {
            if (nametag != null)
            {
                nametag.VoiceChat = nametag.Speaking = nametag.Hushed =
                    nametag.ShowMessage = nametag.DM = nametag.Mention = nametag.Community =
                        nametag.SceneAvatarTagVisible = false;

                nametag.NameVisible = true;
            }
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnEnable() =>
            nametag = GetComponent<UIDocument>().rootVisualElement.Q<NametagElement>();
    }
}

using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Nametags
{
    [RequireComponent(typeof(UIDocument))]
    public class NametagHolder: MonoBehaviour
    {
        public NametagElement Nametag { get; private set; }

        private void OnEnable() =>
            Nametag = GetComponent<UIDocument>().rootVisualElement.Q<NametagElement>();

        // Visual flags live as CSS classes on the NametagElement, and the debug label as its text; both persist across pool reuse.
        // Reset transient state on release so a freshly-acquired holder cannot inherit a previous owner's voice chat badge, chat bubble or debug label.
        public void ResetTransientVisualState()
        {
            if (Nametag == null)
                return;

            Nametag.VoiceChat = Nametag.Speaking = Nametag.Hushed =
                Nametag.ShowMessage = Nametag.DM = Nametag.Mention = Nametag.Community = false;

            Nametag.DebugText = null;
        }
    }
}

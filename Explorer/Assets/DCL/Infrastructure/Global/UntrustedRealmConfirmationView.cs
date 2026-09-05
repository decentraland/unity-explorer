using MVC;
using TMPro;
using UnityEngine;

namespace DCL.Infrastructure.Global
{
    public class UntrustedRealmConfirmationView : ViewBase, IView
    {
        [field: SerializeField]
        public UnityEngine.UI.Button CloseButton { get; set; }
        [field: SerializeField]
        public UnityEngine.UI.Button ContinueButton { get; set; }
        [field: SerializeField]
        public UnityEngine.UI.Button QuitButton { get; set; }
        [field: SerializeField]
        public TMP_Text RealmLabel { get; set; }
    }
}

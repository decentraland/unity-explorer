using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

namespace DCL.Backpack.EmotesSection
{
    public class BackpackEmoteBreadCrumbView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text SlotLabel { get; private set; }
        [field: SerializeField] public LocalizeStringEvent CategoryLabel { get; private set; }
    }
}

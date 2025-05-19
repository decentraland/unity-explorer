using DCL.UI;
using UnityEngine;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserView : MonoBehaviour
    {
        [field: Header("Animators")]
        [field: SerializeField] internal Animator panelAnimator { get; private set; }
        [field: SerializeField] internal Animator headerAnimator { get; private set; }

        [field: Header("Header")]
        [field: SerializeField] internal DropdownView sortByDropdown { get; private set; }

        [field: Header("Results")]
        [field: SerializeField] internal GameObject communityCardPrefab { get; private set; }
        [field: SerializeField] internal Transform resultsContainer { get; private set; }
    }
}

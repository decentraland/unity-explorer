using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class HideCategoryView : MonoBehaviour
    {
        [field: SerializeField]
        internal Image categoryImage { get; private set; }

        [field: SerializeField]
        internal TMP_Text categoryText { get; private set; }
    }
}

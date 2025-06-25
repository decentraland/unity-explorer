using TMPro;
using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsRowView : MonoBehaviour
    {
        [field: SerializeField]
        public SpecCategory Category { get; private set; }

        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI requiredText;
        [SerializeField] private TextMeshProUGUI actualText;

        public void Set(SpecResult result)
        {
            titleText.text = result.Category.ToString();
            requiredText.text = result.Required;

            string icon = result.IsMet ? "<sprite name=\"check\">" : "<sprite name=\"cross\">";
            actualText.text = $"{icon} {result.Actual}";
        }

        public void Clear()
        {
            titleText.text = "";
            requiredText.text = "";
            actualText.text = "";
        }
    }
}
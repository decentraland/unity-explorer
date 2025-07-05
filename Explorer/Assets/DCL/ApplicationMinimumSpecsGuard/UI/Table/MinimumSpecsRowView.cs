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

        public void SetTitle(string text)
        {
            titleText.text = text;
        }

        public void SetRequiredText(string text)
        {
            requiredText.text = text;
        }

        public void SetActualText(string text)
        {
            actualText.text = text;
        }
    }
}
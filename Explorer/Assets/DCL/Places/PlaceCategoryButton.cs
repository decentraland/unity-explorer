using DCL.UI;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Places
{
    public class PlaceCategoryButton : MonoBehaviour
    {
        [SerializeField] private Image iconImage = null!;
        [SerializeField] private TMP_Text text = null!;
        [SerializeField] private PlaceCategoryIconsMapping categoryIconsMapping = null!;
        [SerializeField] public ButtonWithSelectableStateView buttonView = null!;

        public void Configure(string categoryId, string categoryName)
        {
            text.text = RemoveNonASCIICharacters(categoryName).ToUpper().Trim();
            iconImage.sprite = categoryIconsMapping.GetCategoryImage(categoryId);
            iconImage.gameObject.SetActive(iconImage.sprite != null);
        }

        private static string RemoveNonASCIICharacters(string text)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in text)
                if (c <= 127) sb.Append(c);

            return sb.ToString();
        }
    }
}

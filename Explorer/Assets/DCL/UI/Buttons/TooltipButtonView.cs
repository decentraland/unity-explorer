using DCL.UI.Buttons;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public class TooltipButtonView : MonoBehaviour
    {
        [SerializeField] public TextMeshProUGUI TooltipText;

        public void Activate(string tooltipText)
        {
            TooltipText.text = tooltipText;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }
    }
}
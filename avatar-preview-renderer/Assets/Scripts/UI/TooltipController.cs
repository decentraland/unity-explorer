using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class TooltipController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private float tooltipDelay = 0.5f;
        [SerializeField] private float tooltipPadding;

        private VisualElement _root;
        private Label _tooltip;

        private VisualElement _tooltipTarget;
        private Coroutine _tooltipDelayCoroutine;
        private Vector2 _lastMousePosition;

        private void OnEnable()
        {
            _root = uiDocument.rootVisualElement;
            _tooltip = _root.Q<Label>("Tooltip");

            _root.RegisterCallback<PointerEnterEvent>(OnPointerEnter, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (_tooltipTarget != null) return;

            var element = evt.target as VisualElement;
            if (element == null || string.IsNullOrEmpty(element.tooltip))
                return;

            _tooltip.text = element.tooltip;
            _tooltipTarget = element;

            PositionTooltip(evt.position);
            _tooltipDelayCoroutine = StartCoroutine(ShowTooltipWithDelay());
        }

        private IEnumerator ShowTooltipWithDelay()
        {
            yield return new WaitForSeconds(tooltipDelay);

            _tooltip.style.visibility = Visibility.Visible;

            PositionTooltip(_lastMousePosition);
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (_tooltipTarget == null || _tooltipTarget != evt.target) return;

            if (_tooltipDelayCoroutine != null)
            {
                StopCoroutine(_tooltipDelayCoroutine);
                _tooltipDelayCoroutine = null;
            }

            _tooltipTarget = null;

            _tooltip.style.visibility = Visibility.Hidden;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            _lastMousePosition = evt.position;
            PositionTooltip(evt.position);
        }

        private void PositionTooltip(Vector2 mousePosition)
        {
            var tooltipSize = _tooltip.contentRect.size;

            var x = mousePosition.x - tooltipSize.x - tooltipPadding;
            var y = mousePosition.y + tooltipSize.y + tooltipPadding;
            
            // TODO

            // Check if the tooltip goes off the right side of the screen
            // if (x + tooltipSize.x > _root.resolvedStyle.width)
            // {
            //     x = mousePosition.x - tooltipSize.x - tooltipPadding; // Adjust to the left
            // }
            //
            // // Check if the tooltip goes off the left side of the screen
            // if (x < 0)
            // {
            //     x = tooltipPadding; // Ensure it stays within the left side of the screen
            // }
            //
            // // Check if the tooltip goes off the bottom side of the screen
            // if (y + tooltipSize.y > _root.resolvedStyle.height)
            // {
            //     y = mousePosition.y - tooltipSize.y - tooltipPadding; // Adjust to the top
            // }
            //
            // // Check if the tooltip goes off the top side of the screen
            // if (y < 0)
            // {
            //     y = tooltipPadding; // Ensure it stays within the top side of the screen
            // }

            _tooltip.style.left = x;
            _tooltip.style.top = y;
        }
    }
}
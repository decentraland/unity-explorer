using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public static class RectTransformExtensions
    {
        public static async UniTaskVoid ForceUpdateLayoutAsync(this RectTransform rt, CancellationToken ct, bool delayed = true)
        {
            if (!rt.gameObject.activeInHierarchy)
                return;

            if (delayed)
                await UniTask.NextFrame(cancellationToken: ct);

            InverseTransformChildTraversal<RectTransform>(ForceRebuildLayoutImmediate, rt);
        }

        /// <summary>
        /// Reimplementation of the LayoutRebuilder.ForceRebuildLayoutImmediate() function (Unity UI API) for make it more performant.
        /// </summary>
        /// <param name="rectTransformRoot">Root from which to rebuild.</param>
        public static void ForceRebuildLayoutImmediate(RectTransform rectTransformRoot)
        {
            if (rectTransformRoot == null)
                return;

            var layoutElements = rectTransformRoot.GetComponentsInChildren(typeof(ILayoutElement), true).ToList();
            layoutElements.RemoveAll(e => e is Behaviour { isActiveAndEnabled: false } or TextMeshProUGUI);

            foreach (var layoutElem in layoutElements)
            {
                (layoutElem as ILayoutElement)?.CalculateLayoutInputHorizontal();
                (layoutElem as ILayoutElement)?.CalculateLayoutInputVertical();
            }

            var layoutControllers = rectTransformRoot.GetComponentsInChildren(typeof(ILayoutController), true).ToList();
            layoutControllers.RemoveAll(e => e is Behaviour && !((Behaviour)e).isActiveAndEnabled);

            foreach (var layoutCtrl in layoutControllers)
            {
                (layoutCtrl as ILayoutController)?.SetLayoutHorizontal();
                (layoutCtrl as ILayoutController)?.SetLayoutVertical();
            }
        }

        public static void InverseTransformChildTraversal<TComponent>(Action<TComponent> action, Transform startTransform)
            where TComponent: Component
        {
            if (startTransform == null)
                return;

            foreach (Transform t in startTransform)
                InverseTransformChildTraversal(action, t);

            var component = startTransform.GetComponent<TComponent>();

            if (component != null)
                action.Invoke(component);
        }
    }
}

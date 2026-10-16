using UnityEngine;

namespace DCL.UI
{
    public static class SectionHosting
    {
        /// <summary>Moves a section view under a host, stretched to fill it.</summary>
        /// <returns>False when the view already sits under the host.</returns>
        public static bool MoveUnder(RectTransform view, RectTransform host)
        {
            if (view.parent == host) return false;

            view.SetParent(host, false);
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.offsetMin = Vector2.zero;
            view.offsetMax = Vector2.zero;
            return true;
        }
    }
}

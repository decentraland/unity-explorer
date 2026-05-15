using UnityEngine;

namespace DCL.Settings.Settings
{
    [CreateAssetMenu(fileName = "PointAtMarkerVisibilitySettings", menuName = "DCL/Settings/Point at marker Settings")]
    public class PointAtMarkerVisibilitySettings : ScriptableObject
    {
        public VisibilitySetting MarkerVisibilitySetting = VisibilitySetting.FRIENDS_ONLY;

        public void SetMarkerVisibility(VisibilitySetting visibilitySetting) =>
            MarkerVisibilitySetting = visibilitySetting;

        public enum VisibilitySetting
        {
            FRIENDS_ONLY = 0,
            ALL = 1,
            NONE = 2,
        }
    }
}

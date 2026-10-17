using UnityEngine;

namespace DCL.Settings.Settings
{
    [CreateAssetMenu(fileName = "PointAtMarkerVisibilitySettings", menuName = "DCL/Settings/Point at marker Settings")]
    public class PointAtMarkerVisibilitySettings : ScriptableObject
    {
        public VisibilitySetting MarkerVisibilitySetting = VisibilitySetting.FriendsOnly;

        public void SetMarkerVisibility(VisibilitySetting visibilitySetting) =>
            MarkerVisibilitySetting = visibilitySetting;

        public enum VisibilitySetting
        {
            FriendsOnly = 0,
            All = 1,
            None = 2,
        }
    }
}

using DCL.ECSComponents;

namespace ECS.Unity.Visibility
{
    public static class PBVisibilityComponentDefaults
    {
        /// <summary>
        /// If no data is written into the component by default returns "true"
        /// </summary>
        public static bool GetVisible(this PBVisibilityComponent self) =>
            !self.HasVisible || self.Visible;
    }
}

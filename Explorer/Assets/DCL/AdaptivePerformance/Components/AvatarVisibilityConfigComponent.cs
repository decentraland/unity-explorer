namespace DCL.Optimization.AdaptivePerformance.Components
{
    /// <summary>
    /// ECS singleton component that holds avatar visibility configuration.
    /// Updated by AvatarCountScaler and AvatarDistanceScaler based on performance.
    /// Read by AvatarShapeVisibilitySystem to apply dynamic culling.
    /// </summary>
    public struct AvatarVisibilityConfigComponent
    {
        /// <summary>
        /// Maximum number of avatars to render simultaneously.
        /// Range: 10-60 avatars (controlled by AvatarCountScaler)
        /// </summary>
        public int MaxVisibleAvatars;

        /// <summary>
        /// Maximum distance (in meters) at which avatars are visible.
        /// Range: 20-60 meters (controlled by AvatarDistanceScaler)
        /// </summary>
        public float MaxVisibilityDistance;
    }
}

namespace ECS
{
    /// <summary>
    ///     Metadata describing a PX.
    ///     Depending on how the PX was created, the meaning of each property might differ.
    /// </summary>
    public struct PortableExperienceMetadata
    {
        public PortableExperienceType Type;

        public string Ens;

        public string Id;

        public string Name;

        public string ParentSceneId;
    }

    public enum PortableExperienceType
    {
        /// <summary>
        ///     A PX loaded explicitly by the user.
        /// </summary>
        LOCAL,

        /// <summary>
        ///     A PX that runs for all users.
        /// </summary>
        GLOBAL,

        /// <summary>
        ///     A PX attached to a Smart Wearable.
        /// </summary>
        SMART_WEARABLE
    }
}

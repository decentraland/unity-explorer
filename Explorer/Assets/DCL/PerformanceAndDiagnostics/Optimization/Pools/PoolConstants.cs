namespace DCL.Optimization.Pools
{
    public static class PoolConstants
    {
        public const bool CHECK_COLLECTIONS =
            #if DEBUG_POOLS
true;
            #else
false;
            #endif

        /// <summary>
        ///     Initial capacity of pools that should exist per scene context
        /// </summary>
        public const int SCENES_COUNT = 50;

        /// <summary>
        ///     Initial capacity of pools that should exist for global context
        /// </summary>
        public const int GLOBAL_WORLD_COUNT = 50;

        /// <summary>
        ///     Initial capacity of pools that should exist per empty scene context
        /// </summary>
        public const int EMPTY_SCENES_COUNT = 400;

        /// <summary>
        ///     The maximum number of scenes before everything explodes according to our expectations
        /// </summary>
        public const int SCENES_MAX_CAPACITY = 300;

        /// <summary>
        ///     Initial capacity of pools connected to the total number of entities per scene
        /// </summary>
        public const int ENTITIES_COUNT_PER_SCENE = 2000;

        /// <summary>
        ///     initial capacity of pools and collections that exist per SDK component type
        /// </summary>
        public const int SDK_COMPONENT_TYPES_COUNT = 30;

        /// <summary>
        ///     prewarmed capacity for the skinning compute shade
        /// </summary>
        public const int COMPUTE_SHADER_COUNT = 30;

        /// <summary>
        ///     Target simultaneous Avatars Count
        /// </summary>
        public const int AVATARS_COUNT = 100;

        /// <summary>
        ///     The base number of wearables on a single avatar
        /// </summary>
        public const int WEARABLES_PER_AVATAR_COUNT = 15;
    }
}

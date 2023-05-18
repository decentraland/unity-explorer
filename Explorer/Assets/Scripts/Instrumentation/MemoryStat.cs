using Unity.Profiling;

namespace Instrumentation
{
    /// <summary>
    ///     Memory stats that is possible to track by <see cref="ProfilerRecorder" /> <br />
    ///     See https://docs.unity3d.com/2022.2/Documentation/Manual/ProfilerMemory.html
    /// </summary>
    public static class MemoryStat
    {
        /// <summary>
        ///     The .NET runtime imposes a minimum object size, which is 12 bytes on a 32-bit system and 24 bytes on a 64-bit system.
        ///     Even if the class has no fields, the object will still occupy at least this minimum size.
        /// </summary>
        public const int MINIMUM_OBJECT_SIZE = 24;

        public static readonly ProfilerCategory CATEGORY = ProfilerCategory.Memory;

        private static ProfilerStat FromStatName(string statName) =>
            new (CATEGORY, statName);

        /// <summary>
        ///     Stats available in Release builds
        /// </summary>
        public static class Release
        {
            /// <summary>
            ///     The length of the Total Committed Memory bar indicates the total amount of memory that Unity’s Memory Manager system tracked,
            ///     how much of that it used, and how much memory isn’t tracked through this system.
            /// </summary>
            public static readonly ProfilerStat TOTAL_COMMITTED_MEMORY = FromStatName("System Used Memory");

            /// <summary>
            ///     Indicates the total amount of memory that Unity uses and tracks
            /// </summary>
            public static readonly ProfilerStat TOTAL_USED_MEMORY = FromStatName("Total Used Memory");

            /// <summary>
            ///     The amount of memory that Unity reserves for tracking purposes and pool allocations
            /// </summary>
            public static readonly ProfilerStat TOTAL_RESERVED_MEMORY = FromStatName("Total Reserved Memory");

            /// <summary>
            ///     The used heap size and total heap size that managed code uses.
            /// </summary>
            public static readonly ProfilerStat GC_USED_MEMORY = FromStatName("GC Used Memory");

            /// <summary>
            ///     The used heap size and total heap size that managed code uses.
            /// </summary>
            public static readonly ProfilerStat GC_RESERVED_MEMORY = FromStatName("GC Reserved Memory");

            /// <summary>
            ///     The Audio system’s estimated memory usage.
            /// </summary>
            public static readonly ProfilerStat AUDIO_USED_MEMORY = FromStatName("Audio Used Memory");

            /// <summary>
            ///     The Audio system’s estimated memory usage.
            /// </summary>
            public static readonly ProfilerStat AUDIO_RESERVED_MEMORY = FromStatName("Audio Reserved Memory");

            /// <summary>
            ///     The Video system’s estimated memory usage.
            /// </summary>
            public static readonly ProfilerStat VIDEO_USED_MEMORY = FromStatName("Video Used Memory");

            /// <summary>
            ///     The Video system’s estimated memory usage.
            /// </summary>
            public static readonly ProfilerStat VIDEO_RESERVED_MEMORY = FromStatName("Video Reserved Memory");
        }

        /// <summary>
        ///     Stats available in Debug builds and Editor only
        /// </summary>
        public static class Debug
        {
            /// <summary>
            ///     Displays the amount of object instances of the types of Assets that commonly take up a high percentage of the memory
            ///     (Textures, Meshes, Materials, Animation Clips), together with their accumulated sizes in memory (Assets, GameObjects, Scene Objects).
            /// </summary>
            public static readonly ProfilerStat OBJECT_COUNT = FromStatName("Object Count");
            /// <summary>
            ///     The total count of loaded textures and memory they use.
            /// </summary>
            public static readonly ProfilerStat TEXTURE_COUNT = FromStatName("Texture Count");
            /// <summary>
            ///     The total count of loaded textures and memory they use.
            /// </summary>
            public static readonly ProfilerStat TEXTURE_MEMORY = FromStatName("Texture Memory");
            /// <summary>
            ///     The total count of loaded meshes and memory they use.
            /// </summary>
            public static readonly ProfilerStat MESH_COUNT = FromStatName("Mesh Count");
            /// <summary>
            ///     The total count of loaded meshes and memory they use.
            /// </summary>
            public static readonly ProfilerStat MESH_MEMORY = FromStatName("Mesh Memory");
            /// <summary>
            ///     The total count of loaded materials and memory they use.
            /// </summary>
            public static readonly ProfilerStat MATERIAL_COUNT = FromStatName("Material Count");
            /// <summary>
            ///     The total count of loaded materials and memory they use.
            /// </summary>
            public static readonly ProfilerStat MATERIAL_MEMORY = FromStatName("Material Memory");

            /// <summary>
            ///     Displays the amount of managed allocations in the selected frame
            /// </summary>
            public static readonly ProfilerStat GC_ALLOCATED_IN_FRAME_COUNT = FromStatName("GC Allocation In Frame Count");

            /// <summary>
            ///     Displays the total size in bytes of managed allocations in the selected frame
            /// </summary>
            public static readonly ProfilerStat GC_ALLOCATED_IN_FRAME = FromStatName("GC Allocated In Frame");
        }
    }
}

﻿namespace Diagnostics.ReportsHandling
{
    /// <summary>
    ///     Contains names of report categories, IDs must be constant so they can be specified in the attribute
    /// </summary>
    public static class ReportCategory
    {
        /// <summary>
        ///     Non-granular engine category
        /// </summary>
        public const string ENGINE = nameof(ENGINE);

        /// <summary>
        ///     CRDT Related messages
        /// </summary>
        public const string CRDT = nameof(CRDT);

        /// <summary>
        ///     Messages related to conversion between CRDT and ECS World
        /// </summary>
        public const string CRDT_ECS_BRIDGE = nameof(CRDT_ECS_BRIDGE);

        /// <summary>
        ///     Everything connected to realms
        /// </summary>
        public const string REALM = nameof(REALM);

        /// <summary>
        ///     Messages related to the scene creation process
        /// </summary>
        public const string SCENE_FACTORY = nameof(SCENE_FACTORY);

        /// <summary>
        ///     Messages related to the scene loading and destruction processes initiated in the global world
        /// </summary>
        public const string SCENE_LOADING = nameof(SCENE_LOADING);

        /// <summary>
        ///     Errors reported from JavaScript
        /// </summary>
        public const string JAVASCRIPT = nameof(JAVASCRIPT);

        /// <summary>
        ///     Unspecified ECS World Exceptions
        /// </summary>
        public const string ECS = nameof(ECS);

        /// <summary>
        ///     Non-granular Streamable category
        /// </summary>
        public const string STREAMABLE_LOADING = nameof(STREAMABLE_LOADING);

        /// <summary>
        ///     Everything related to asset bundles
        /// </summary>
        public const string ASSET_BUNDLES = nameof(ASSET_BUNDLES);

        /// <summary>
        ///     Everything related to textures
        /// </summary>
        public const string TEXTURES = nameof(TEXTURES);

        /// <summary>
        ///     Everything related to GLTF
        /// </summary>
        public const string GLTF_CONTAINER = nameof(GLTF_CONTAINER);

        /// <summary>
        ///     Everything related to materials
        /// </summary>
        public const string MATERIALS = nameof(MATERIALS);

        /// <summary>
        ///     Everything related to primitive colliders
        /// </summary>
        public const string PRIMITIVE_COLLIDERS = nameof(PRIMITIVE_COLLIDERS);

        /// <summary>
        ///     Everything related to primitive meshes
        /// </summary>
        public const string PRIMITIVE_MESHES = nameof(PRIMITIVE_MESHES);

        /// <summary>
        ///     Default category into which falls everything that is reported without info and by default Unity Debug.Log
        /// </summary>
        public const string UNSPECIFIED = nameof(UNSPECIFIED);
    }
}

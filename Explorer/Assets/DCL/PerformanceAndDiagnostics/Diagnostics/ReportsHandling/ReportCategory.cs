﻿using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Contains names of report categories, IDs must be constant so they can be specified in the attribute
    /// </summary>
    public static class ReportCategory
    {
        public const string FEATURE_FLAGS = nameof(FEATURE_FLAGS);

        /// <summary>
        ///     Everything connected to raw assets and addressables
        /// </summary>
        public const string ANALYTICS = nameof(ANALYTICS);

        /// <summary>
        ///     Everything connected to raw assets and addressables
        /// </summary>
        public const string ASSETS_PROVISION = nameof(ASSETS_PROVISION);

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
        ///     Messages related to the scene UI
        /// </summary>
        public const string SCENE_UI = nameof(SCENE_UI);

        /// <summary>
        ///     Messages related to highlighting objects in the scene on hover
        /// </summary>
        public const string HIGHLIGHTS = nameof(HIGHLIGHTS);

        /// <summary>
        ///     Errors reported from JavaScript
        /// </summary>
        public const string JAVASCRIPT = nameof(JAVASCRIPT);

        /// <summary>
        ///     Archipelago requests
        /// </summary>
        public const string ARCHIPELAGO_REQUEST = nameof(ARCHIPELAGO_REQUEST);

        /// <summary>
        ///     Unspecified ECS World Exceptions
        /// </summary>
        public const string ECS = nameof(ECS);

        /// <summary>
        ///     Unspecified web request
        /// </summary>
        public const string GENERIC_WEB_REQUEST = nameof(GENERIC_WEB_REQUEST);

        /// <summary>
        ///     Texture related web request
        /// </summary>
        public const string TEXTURE_WEB_REQUEST = nameof(TEXTURE_WEB_REQUEST);

        /// <summary>
        ///     Texture related web request
        /// </summary>
        public const string NFT_SHAPE_WEB_REQUEST = nameof(NFT_SHAPE_WEB_REQUEST);

        /// <summary>
        ///     Nft info related web request
        /// </summary>
        public const string NFT_INFO_WEB_REQUEST = nameof(NFT_INFO_WEB_REQUEST);

        /// <summary>
        ///     Audio clip related web request
        /// </summary>
        public const string AUDIO_CLIP_WEB_REQUEST = nameof(AUDIO_CLIP_WEB_REQUEST);

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
        ///     Everything related to Scenes audio source components
        /// </summary>
        public const string SDK_AUDIO_SOURCES = nameof(SDK_AUDIO_SOURCES);

        /// <summary>
        ///     Everything related to Media streaming components such as PBAudioStream or PBVideoPlayer
        /// </summary>
        public const string MEDIA_STREAM = nameof(MEDIA_STREAM);

        /// <summary>
        ///     Everything related to prioritization
        /// </summary>
        public const string PRIORITIZATION = nameof(PRIORITIZATION);

        /// <summary>
        ///     Everything related to the player's motion
        /// </summary>
        public const string MOTION = nameof(MOTION);

        /// <summary>
        ///     Everything related to the procedural landscape generation
        /// </summary>
        public const string LANDSCAPE = nameof(LANDSCAPE);

        /// <summary>
        ///     Input
        /// </summary>
        public const string INPUT = nameof(INPUT);

        /// <summary>
        ///     Avatar rendering
        /// </summary>
        public const string AVATAR = nameof(AVATAR);

        public const string PROFILE = nameof(PROFILE);

        public const string TWEEN = nameof(TWEEN);

        public const string ANIMATOR = nameof(ANIMATOR);

        /// <summary>
        ///     Wearable related
        /// </summary>
        public const string WEARABLE = nameof(WEARABLE);
        public const string EMOTE = nameof(EMOTE);
        public const string THUMBNAILS = nameof(THUMBNAILS);

        public const string AUTHENTICATION = nameof(AUTHENTICATION);

        public const string LOD = nameof(LOD);

        /// <summary>
        ///     AvatarAttach SDK component
        /// </summary>
        public const string AVATAR_ATTACH = nameof(AVATAR_ATTACH);

        /// <summary>
        ///     Quality related logs
        /// </summary>
        public const string QUALITY = nameof(QUALITY);

        /// <summary>
        ///     Scene's restricted actions
        /// </summary>
        public const string RESTRICTED_ACTIONS = nameof(RESTRICTED_ACTIONS);

        /// <summary>
        ///     Avatars Trigger Area for SDK components (e.g. CameraModeArea, AvatarModifierArea)
        /// </summary>
        public const string CHARACTER_TRIGGER_AREA = nameof(CHARACTER_TRIGGER_AREA);

        /// <summary>
        ///     SDK Camera related systems
        /// </summary>
        public const string SDK_CAMERA = nameof(SDK_CAMERA);

        /// <summary>
        ///     Player-related data propagated to SDK
        /// </summary>
        public const string PLAYER_SDK_DATA = nameof(PLAYER_SDK_DATA);

        /// <summary>
        ///     RealmData 'IsDirty' flag reset system
        /// </summary>
        public const string REALM_DATA_DIRTY_RESET_SYSTEM = nameof(REALM_DATA_DIRTY_RESET_SYSTEM);

        /// <summary>
        ///     SDK Observables layer
        /// </summary>
        public const string SDK_OBSERVABLES = nameof(SDK_OBSERVABLES);

        /// <summary>
        ///     Local scene development connection with locally running scene (AKA "Preview Mode")
        /// </summary>
        public const string SDK_LOCAL_SCENE_DEVELOPMENT = nameof(SDK_LOCAL_SCENE_DEVELOPMENT);

        /// <summary>
        ///     Roads related systems
        /// </summary>
        public const string ROADS = nameof(ROADS);

        public const string LIVEKIT = nameof(LIVEKIT);

        public const string MULTIPLAYER_MOVEMENT = nameof(MULTIPLAYER_MOVEMENT);
        public const string MULTIPLAYER = nameof(MULTIPLAYER);

        public const string AUDIO = nameof(AUDIO);

        /// <summary>
        ///     Generic UI category
        /// </summary>
        public const string UI = nameof(UI);

        /// <summary>
        ///     Fetch requests initiated from the Scene side through the fetch API
        /// </summary>
        public const string SCENE_FETCH_REQUEST = nameof(SCENE_FETCH_REQUEST);

        public const string MVC = nameof(MVC);

        public const string SETTINGS_MENU = nameof(SETTINGS_MENU);

        public const string BACKPACK = nameof(BACKPACK);

        public const string BADGES = nameof(BADGES);

        /// <summary>
        ///     Whatever is coming from external pure C# plugins
        /// </summary>
        public const string PLUGINS = nameof(PLUGINS);

        /// <summary>
        ///     To show intended debug messages
        /// </summary>
        public const string DEBUG = nameof(DEBUG);

        /// <summary>
        ///     For startup operations flow
        /// </summary>
        public const string STARTUP = nameof(STARTUP);

        /// <summary>
        ///     Default category into which falls everything that is reported without info and by default Unity Debug.Log
        /// </summary>
        public const string UNSPECIFIED = nameof(UNSPECIFIED);

        public const string PORTABLE_EXPERIENCE= nameof(PORTABLE_EXPERIENCE);

    }
}

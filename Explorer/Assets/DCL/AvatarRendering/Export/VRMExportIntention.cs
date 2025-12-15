using System;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Export
{
    /// <summary>
    /// Tag to mark an entity for VRM export processing.
    /// Added alongside AvatarShapeComponent to trigger export-specific instantiation.
    /// It's consumed by AvatarInstantiatorSystem.InstantiateExportAvatar.
    /// </summary>
    public struct VRMExportIntention
    {
        public string AuthorName { get; set; }
        public string SavePath { get; set; }
        public Action OnFinishedAction { get; set; }
    }
    
    /// <summary>
    /// Created after export avatar is instantiated. Contains all data needed for VRM export.
    /// It's consumed by ExportAvatarSystem.
    /// </summary>
    public struct VRMExportDataComponent
    {
        public AvatarBase AvatarBase { get; set; }
        public List<CachedAttachment> InstantiatedWearables { get; set; }
        
        public Color SkinColor { get; set; }
        public Color HairColor { get; set; }
        public Color EyesColor { get; set; }
        
        public Dictionary<string, Texture> FacialFeatureMainTextures { get; set; }
        public Dictionary<string, Texture> FacialFeatureMaskTextures { get; set; }
        
        public List<WearableExportInfo> WearableInfos { get; set; }
        public string AuthorName { get; set; }
        
        /// <summary>
        /// Cleanup action that releases the avatar back to pool and disposes wearables.
        /// Set by AvatarInstantiatorSystem, called by ExportAvatarSystem.
        /// </summary>
        public Action CleanupAction;

        public string SavePath { get; set; }
        public Action OnFinishedAction { get; set; }

        public void Cleanup()
        {
            CleanupAction?.Invoke();
            CleanupAction = null;
        }
    }
    
    public struct WearableExportInfo
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string MarketPlaceUrl { get; set; }
    }
}
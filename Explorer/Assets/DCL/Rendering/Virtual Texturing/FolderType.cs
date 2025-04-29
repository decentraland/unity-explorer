using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace VirtualTexture
{
    /// <summary>
    /// Defines standard folder locations for loading virtual texture data.
    /// 
    /// This enum provides a consistent way to reference common Unity folder paths,
    /// abstracting the actual filesystem paths which may vary across platforms.
    /// </summary>
    public enum FolderType
    {
        /// <summary>
        /// The main Assets folder of the Unity project (Application.dataPath)
        /// </summary>
        ApplicationData,
        
        /// <summary>
        /// The StreamingAssets folder, accessible at runtime across all platforms
        /// </summary>
        StreamingAssets,
        
        /// <summary>
        /// No specific folder, used when a custom path will be provided
        /// </summary>
        None,
    }

    /// <summary>
    /// Extension methods for converting FolderType enum values to actual filesystem paths.
    /// </summary>
    public static class FolderTypeExtensions
    {
        /// <summary>
        /// Converts a FolderType to its corresponding filesystem path.
        /// </summary>
        /// <param name="folder">The folder type to convert</param>
        /// <returns>The absolute filesystem path for the specified folder type</returns>
        public static string ToStr(this FolderType folder)
        {
            switch(folder)
            {
            case FolderType.ApplicationData:
                return Application.dataPath;
            case FolderType.StreamingAssets:
                return Application.streamingAssetsPath;
            }
            return "";
        }
    }
}
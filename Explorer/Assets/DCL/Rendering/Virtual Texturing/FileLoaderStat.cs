using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace VirtualTexture
{
    /// <summary>
    /// Statistics collector for the FileLoader component.
    /// 
    /// Tracks metrics about file loading operations, including request counts,
    /// failure rates, and total data transferred. Used for monitoring and debugging
    /// the virtual texturing system's loading performance.
    /// </summary>
    public class FileLoaderStat
    {
        /// <summary>
        /// Number of requests currently being processed or queued.
        /// </summary>
        public int CurrentRequestCount { get; set; }

        /// <summary>
        /// Total number of requests made since the application started.
        /// </summary>
        public int TotalRequestCount { get; set; }

        /// <summary>
        /// Total number of failed requests since the application started.
        /// </summary>
        public int TotalFailCount { get; set; }

        /// <summary>
        /// Total amount of texture data downloaded in megabytes.
        /// </summary>
        public float TotalDownladSize { get; set; }
    }
}
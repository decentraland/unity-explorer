using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace VirtualTexture
{
    /// <summary>
    /// Loads virtual texture pages from files on disk.
    /// 
    /// The FileLoader handles loading texture data from the file system based on
    /// page coordinates and mipmap levels. It manages concurrent load operations
    /// with configurable limits and queuing.
    /// </summary>
    public class FileLoader : MonoBehaviour, ILoader
    {
        /// <summary>
        /// Event fired when a load request completes, providing the loaded textures.
        /// </summary>
        public event Action<LoadRequest, Texture2D[]> OnLoadComplete;

        /// <summary>
        /// Maximum number of concurrent file loads allowed.
        /// Higher values may improve throughput but increase memory usage.
        /// </summary>
        [SerializeField]
        private int m_ThreadLimit = 1;

        /// <summary>
        /// Root directory where texture files are stored.
        /// </summary>
        [SerializeField]
        private FolderType m_FileRoot = default;

        /// <summary>
        /// Format strings for constructing file paths.
        /// Each string represents a different texture layer (diffuse, normal, etc.).
        /// Paths should include placeholders for page coordinates and mipmap level.
        /// </summary>
        [SerializeField]
        private string[] m_FilePathStrs = default;

        /// <summary>
        /// Requests that are currently being processed.
        /// </summary>
        private List<LoadRequest> m_RuningRequests = new List<LoadRequest>();

        /// <summary>
        /// Requests that are waiting to be processed.
        /// </summary>
        private List<LoadRequest> m_PendingRequests = new List<LoadRequest>();

        /// <summary>
        /// Statistics about file loading operations.
        /// </summary>
        public FileLoaderStat Stat { get; } = new FileLoaderStat();

        private void Start()
        {
            // Construct full file paths by combining the root directory with path templates
            for(int i = 0; i < m_FilePathStrs.Length; i++)
            {
                // Prepend "file:///" to ensure paths work with UnityWebRequest
                m_FilePathStrs[i] = "file:///" + Path.Combine(m_FileRoot.ToStr(), m_FilePathStrs[i]);
            }
        }

        private void Update()
        {
            Stat.CurrentRequestCount = m_PendingRequests.Count + m_RuningRequests.Count;

            if(m_PendingRequests.Count <= 0)
                return;

            if(m_RuningRequests.Count > m_ThreadLimit)
                return;

            // Sort pending requests by mipmap level, prioritizing higher mipmap levels
            // (which represent lower resolution textures, typically needed for distant objects)
            m_PendingRequests.Sort((x, y) => { return x.MipLevel.CompareTo(y.MipLevel); });

            // Move the highest priority request from pending to running queue
            var req = m_PendingRequests[m_PendingRequests.Count - 1];
            m_PendingRequests.RemoveAt(m_PendingRequests.Count - 1);
            m_RuningRequests.Add(req);

            // Start the coroutine to load the texture files
            StartCoroutine(Load(req));
        }

        /// <summary>
        /// Coroutine that loads texture files for a given request.
        /// </summary>
        /// <param name="request">The request containing page coordinates and mipmap level</param>
        private IEnumerator Load(LoadRequest request)
        {
            Texture2D[] textures = new Texture2D[m_FilePathStrs.Length];

            for(int i = 0; i < m_FilePathStrs.Length; i++)
            {
                // Construct the file path using the format string and request parameters
                // Shift coordinates based on mipmap level (dividing by 2^mip)
                var file = string.Format(m_FilePathStrs[i], 
                                         request.PageX >> request.MipLevel, 
                                         request.PageY >> request.MipLevel, 
                                         request.MipLevel);
                
                // Use UnityWebRequest for async file loading
                var www = UnityWebRequestTexture.GetTexture(file);
                yield return www.SendWebRequest();

                if(!www.isNetworkError && !www.isHttpError)
                {
                    textures[i] = ((DownloadHandlerTexture)www.downloadHandler).texture;
                    // Track data size for statistics (converted to MB)
                    Stat.TotalDownladSize += (float)www.downloadedBytes / 1024.0f / 1024.0f;
                }
                else
                {
                    Debug.LogWarningFormat("Load file({0}) failed: {1}", file, www.error);
                    Stat.TotalFailCount++;
                }
            }

            // Remove from running queue and notify completion
            m_RuningRequests.Remove(request);
            OnLoadComplete?.Invoke(request, textures);
        }

        /// <summary>
        /// Creates a new request to load a specific virtual texture page.
        /// </summary>
        /// <param name="x">X-coordinate in the page table</param>
        /// <param name="y">Y-coordinate in the page table</param>
        /// <param name="mip">Mipmap level to load</param>
        /// <returns>A LoadRequest object, or null if an identical request is already in progress</returns>
        public LoadRequest Request(int x, int y, int mip)
        {
            // Check if an identical request is already being processed
            foreach(var r in m_RuningRequests)
            {
                if(r.PageX == x && r.PageY == y && r.MipLevel == mip)
                    return null;
            }
            
            // Check if an identical request is already pending
            foreach(var r in m_PendingRequests)
            {
                if(r.PageX == x && r.PageY == y && r.MipLevel == mip)
                    return null;
            }

            // Create and queue a new request
            var request = new LoadRequest(x, y, mip);
            m_PendingRequests.Add(request);

            Stat.TotalRequestCount++;

            return request;
        }
    }
}
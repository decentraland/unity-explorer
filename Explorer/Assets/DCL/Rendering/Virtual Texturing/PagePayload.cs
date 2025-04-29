using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Stores the runtime data associated with a virtual texture page.
    /// 
    /// PagePayload tracks the state of a page table entry, including its 
    /// position in the physical tiled texture, when it was last used,
    /// and any pending load requests.
    /// </summary>
    public class PagePayload
    {
        /// <summary>
        /// Special value indicating an invalid tile index
        /// </summary>
        private static Vector2Int s_InvalidTileIndex = new Vector2Int(-1, -1);

        /// <summary>
        /// Index of the tile in the physical tiled texture where this page's data is stored.
        /// Will be s_InvalidTileIndex if the page is not currently loaded.
        /// </summary>
        public Vector2Int TileIndex = s_InvalidTileIndex;

        /// <summary>
        /// Frame number when this page was last activated.
        /// Used to implement LRU (least recently used) policies and track active pages.
        /// </summary>
        public int ActiveFrame;

        /// <summary>
        /// Reference to the load request if this page is being loaded.
        /// Will be null if the page is not currently being loaded.
        /// </summary>
        public LoadRequest LoadRequest;

        /// <summary>
        /// Indicates whether this page has texture data loaded and is ready to use.
        /// </summary>
        public bool IsReady { get { return (TileIndex != s_InvalidTileIndex); } }

        /// <summary>
        /// Resets the page to an unloaded state.
        /// Called when the page's data is evicted from the physical texture.
        /// </summary>
        public void ResetTileIndex()
        {
            TileIndex = s_InvalidTileIndex;
        }
    }
}
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Core component of the virtual texturing system that manages the page table.
    /// 
    /// The page table tracks which virtual texture pages are loaded, at what mipmap levels,
    /// and where they are stored in the physical tiled texture. It processes feedback data
    /// to determine which pages to load, and exports indirection data to shaders.
    /// </summary>
    public class PageTable : MonoBehaviour, IPageTable
    {
        /// <summary>
        /// Size of the page table in each dimension (must be a power of 2).
        /// </summary>
        [SerializeField]
        private int m_TableSize = default;

        /// <summary>
        /// Maximum mipmap level to use, limiting the range of detail levels.
        /// </summary>
        [SerializeField]
        private int m_MipLevelLimit = default;

        /// <summary>
        /// Shader used for visualizing the page table in debug mode.
        /// </summary>
        [SerializeField]
        private Shader m_DebugShader = default;

        /// <summary>
        /// Root node of the page table's quadtree structure.
        /// </summary>
        private TableNode m_PageTable;

        /// <summary>
        /// Dictionary mapping physical tile indices to the page table nodes they contain.
        /// Used to quickly find nodes when tile indices are updated or invalidated.
        /// </summary>
        private Dictionary<Vector2Int, TableNode> m_ActivePages = new Dictionary<Vector2Int, TableNode>();

        /// <summary>
        /// Texture containing indirection data for shaders to locate physical textures.
        /// Each pixel stores tile coordinates and mipmap level information.
        /// </summary>
        private Texture2D m_LookupTexture;

        /// <summary>
        /// Reference to the loader component that fetches texture data.
        /// </summary>
        private ILoader m_Loader;

        /// <summary>
        /// Reference to the tiled texture component that manages physical texture storage.
        /// </summary>
        private ITiledTexture m_TileTexture;

        /// <summary>
        /// Material instance for the debug visualization shader.
        /// </summary>
        private Material m_DebugMaterial;

        /// <summary>
        /// Performance statistics for the page table update operations.
        /// </summary>
        public FrameStat Stat { get; } = new FrameStat();

        /// <summary>
        /// Debug visualization of the page table.
        /// Only available when ENABLE_DEBUG_TEXTURE is defined.
        /// </summary>
        public RenderTexture DebugTexture { get; private set; }

        /// <summary>
        /// Size of the page table in each dimension.
        /// </summary>
        public int TableSize { get { return m_TableSize; } }

        /// <summary>
        /// Maximum supported mipmap level, limited by either the configured limit
        /// or the maximum possible based on table size (log2 of table size).
        /// </summary>
        public int MaxMipLevel { get { return Mathf.Min(m_MipLevelLimit, (int)Mathf.Log(TableSize, 2)); } }

        private void Start()
        {
            // Initialize the page table quadtree with the root node
            m_PageTable = new TableNode(MaxMipLevel, 0, 0, TableSize, TableSize);

            // Create the lookup texture used by shaders for indirection
            m_LookupTexture = new Texture2D(TableSize, TableSize, TextureFormat.RGBA32, false);
            m_LookupTexture.filterMode = FilterMode.Point;
            m_LookupTexture.wrapMode = TextureWrapMode.Clamp;

            // Set global shader parameters for the virtual texturing system
            Shader.SetGlobalTexture(
                "_VTLookupTex",
                m_LookupTexture);
            Shader.SetGlobalVector(
                "_VTPageParam",
                new Vector4(
                    TableSize,
                    1.0f / TableSize,
                    MaxMipLevel,
                    0));

            InitDebugTexture(TableSize, TableSize);

            // Get references to required components
            m_Loader = (ILoader)GetComponent(typeof(ILoader));
            m_Loader.OnLoadComplete += OnLoadComplete;
            m_TileTexture = (ITiledTexture)GetComponent(typeof(ITiledTexture));
            m_TileTexture.OnTileUpdateComplete += InvalidatePage;
            ((IFeedbackReader)GetComponent(typeof(IFeedbackReader))).OnFeedbackReadComplete += ProcessFeedback;
        }

        /// <summary>
        /// Processes feedback data to determine which virtual texture pages to activate.
        /// </summary>
        /// <param name="texture">Texture containing feedback data from the renderer</param>
        private void ProcessFeedback(Texture2D texture)
        {
            Stat.BeginFrame();

            // Process each pixel in the feedback texture to activate corresponding pages
            foreach (var c in texture.GetRawTextureData<Color32>())
            {
                ActivatePage(c.r, c.g, c.b);
            }

            // Update the lookup texture with mappings from virtual to physical texture space
            var currentFrame = (byte)Time.frameCount;
            var pixels = m_LookupTexture.GetRawTextureData<Color32>();
            foreach (var kv in m_ActivePages)
            {
                var page = kv.Value;

                // Only update pages that were active in the current frame
                if (page.Payload.ActiveFrame != Time.frameCount)
                    continue;

                // Pack page data into color channels:
                // R: X-coordinate in tiled texture
                // G: Y-coordinate in tiled texture
                // B: Mipmap level
                // A: Frame counter (to detect stale data)
                var c = new Color32((byte)page.Payload.TileIndex.x, (byte)page.Payload.TileIndex.y, (byte)page.MipLevel, currentFrame);
                for (int y = page.Rect.y; y < page.Rect.yMax; y++)
                {
                    for (int x = page.Rect.x; x < page.Rect.xMax; x++)
                    {
                        var id = y * TableSize + x;
                        if (pixels[id].b > c.b ||  // Write if this mipmap level is more detailed (lower value)
                            pixels[id].a != currentFrame) // or if the pixel hasn't been written in the current frame
                            pixels[id] = c;
                    }
                }
            }
            
            // Apply changes to the lookup texture
            m_LookupTexture.Apply(false);

            Stat.EndFrame();

            UpdateDebugTexture();
        }

        /// <summary>
        /// Activates a virtual texture page at the specified coordinates and mipmap level.
        /// If the exact page isn't available, it uses the best available alternative and 
        /// initiates loading of more detailed pages if needed.
        /// </summary>
        /// <param name="x">X-coordinate in the page table</param>
        /// <param name="y">Y-coordinate in the page table</param>
        /// <param name="mip">Desired mipmap level</param>
        /// <returns>The activated page node, or null if no suitable page was available</returns>
        private TableNode ActivatePage(int x, int y, int mip)
        {
            if (mip > m_PageTable.MipLevel)
                return null;

            // Try to find a suitable page that's already loaded
            var page = m_PageTable.GetAvailable(x, y, mip);
            if (page == null)
            {
                // No suitable page available, request loading of the root node
                LoadPage(x, y, m_PageTable);
                return null;
            }
            else if (page.MipLevel > mip)
            {
                // A lower-detail (higher mip level) page is available
                // Request loading of a more detailed page for next frame
                LoadPage(x, y, page.GetChild(x, y));
            }

            // Mark the tile as active in the physical texture
            m_TileTexture.SetActive(page.Payload.TileIndex);

            // Update the page's active frame to the current frame
            page.Payload.ActiveFrame = Time.frameCount;
            return page;
        }

        /// <summary>
        /// Initiates loading of a virtual texture page if needed.
        /// </summary>
        /// <param name="x">X-coordinate in the page table</param>
        /// <param name="y">Y-coordinate in the page table</param>
        /// <param name="node">The node to load</param>
        private void LoadPage(int x, int y, TableNode node)
        {
            if (node == null)
                return;

            // Skip if already loading
            if(node.Payload.LoadRequest != null)
                return;

            // Create a new load request
            node.Payload.LoadRequest = m_Loader.Request(x, y, node.MipLevel);
        }

        /// <summary>
        /// Callback invoked when a page load operation completes.
        /// Updates the page table with the loaded texture data.
        /// </summary>
        /// <param name="request">The completed load request</param>
        /// <param name="textures">Array of loaded textures for each layer</param>
        private void OnLoadComplete(LoadRequest request, Texture2D[] textures)
        {
            // Find the node corresponding to the load request
            var node = m_PageTable.Get(request.PageX, request.PageY, request.MipLevel);
            if (node == null || node.Payload.LoadRequest != request)
                return;

            // Clear the load request reference
            node.Payload.LoadRequest = null;

            // Request a tile in the physical texture to store the loaded data
            var id = m_TileTexture.RequestTile();
            m_TileTexture.UpdateTile(id, textures);

            // Update the node with the physical tile location
            node.Payload.TileIndex = id;
            m_ActivePages[id] = node;
        }

        /// <summary>
        /// Marks a page as invalid when its tile is reclaimed by the tiled texture.
        /// </summary>
        /// <param name="id">Index of the tile that was reclaimed</param>
        private void InvalidatePage(Vector2Int id)
        {
            TableNode node = null;
            if (!m_ActivePages.TryGetValue(id, out node))
                return;

            // Reset the node's tile index to indicate it's no longer loaded
            node.Payload.ResetTileIndex();
            m_ActivePages.Remove(id);
        }

        /// <summary>
        /// Initializes the debug visualization texture.
        /// Only compiled when ENABLE_DEBUG_TEXTURE is defined.
        /// </summary>
        /// <param name="w">Width of the debug texture</param>
        /// <param name="h">Height of the debug texture</param>
        [Conditional("ENABLE_DEBUG_TEXTURE")]
        private void InitDebugTexture(int w, int h)
        {
            DebugTexture = new RenderTexture(w, h, 0);
            DebugTexture.wrapMode = TextureWrapMode.Clamp;
            DebugTexture.filterMode = FilterMode.Point;
        }

        /// <summary>
        /// Updates the debug visualization texture with current page table state.
        /// Only compiled when ENABLE_DEBUG_TEXTURE is defined.
        /// </summary>
        [Conditional("ENABLE_DEBUG_TEXTURE")]
        private void UpdateDebugTexture()
        {
            if (m_LookupTexture == null || m_DebugShader == null)
                return;

            if (m_DebugMaterial == null)
                m_DebugMaterial = new Material(m_DebugShader);

            DebugTexture.DiscardContents();
            Graphics.Blit(m_LookupTexture, DebugTexture, m_DebugMaterial);
        }
    }
}
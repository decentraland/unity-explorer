using System;
using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Manages physical texture storage for the virtual texturing system.
    /// 
    /// The TiledTexture divides a physical texture into a grid of tiles that can be
    /// dynamically updated with content from different parts of the virtual texture.
    /// It uses an LRU (Least Recently Used) strategy to determine which tiles to reuse
    /// when new content needs to be loaded.
    /// </summary>
    public class TiledTexture : MonoBehaviour, ITiledTexture
    {
        /// <summary>
        /// Event fired when a tile's content has been updated.
        /// </summary>
        public event Action<Vector2Int> OnTileUpdateComplete;

        /// <summary>
        /// The dimensions of the tiled texture in number of tiles.
        /// </summary>
        [SerializeField]
        private Vector2Int m_RegionSize = default;

        /// <summary>
        /// The size of each tile in pixels (excluding padding).
        /// </summary>
        [SerializeField]
        private int m_TileSize = 256;

        /// <summary>
        /// The number of padding pixels around each tile.
        /// </summary>
        [SerializeField]
        private int m_PaddingSize = 4;

        /// <summary>
        /// The number of texture layers managed by this component.
        /// </summary>
        [SerializeField]
        private int m_LayerCount = 1;

        /// <summary>
        /// Shader used for drawing textures into the tiled texture.
        /// </summary>
        [SerializeField]
        private Shader m_DrawTextureShader = default;

        /// <summary>
        /// Material instance for the texture drawing shader.
        /// </summary>
        private Material m_DrawTextureMateral;

        /// <summary>
        /// LRU cache for managing tile allocation and replacement.
        /// </summary>
        private LruCache m_TilePool = new LruCache();

        /// <summary>
        /// Array of physical textures, one for each layer.
        /// </summary>
        public RenderTexture[] Textures { get; private set; }

        /// <summary>
        /// Statistics about tile usage.
        /// </summary>
        public TileTextureStat Stat { get; } = new TileTextureStat();

        /// <summary>
        /// The dimensions of the tiled texture in number of tiles.
        /// </summary>
        public Vector2Int RegionSize { get { return m_RegionSize; } }

        /// <summary>
        /// The size of each tile in pixels (excluding padding).
        /// </summary>
        public int TileSize { get { return m_TileSize; } }

        /// <summary>
        /// The number of padding pixels around each tile.
        /// </summary>
        public int PaddingSize { get { return m_PaddingSize; } }

        /// <summary>
        /// The number of texture layers managed by this component.
        /// </summary>
        public int LayerCount { get { return m_LayerCount; } }

        /// <summary>
        /// The total size of a tile including padding.
        /// </summary>
        public int TileSizeWithPadding { get { return TileSize + PaddingSize * 2; } }

        private void Start()
        {
            // Initialize the tile pool with all available tile indices
            for (int i = 0; i < RegionSize.x * RegionSize.y; i++)
                m_TilePool.Add(i);

            // Create physical textures for each layer
            Textures = new RenderTexture[LayerCount];
            for(int i = 0; i < LayerCount; i++)
            {
                Textures[i] = new RenderTexture(RegionSize.x * TileSizeWithPadding, RegionSize.y * TileSizeWithPadding, 0);
                Textures[i].useMipMap = false;
                Textures[i].wrapMode = TextureWrapMode.Clamp;

                // Expose the physical textures to shaders via global properties
                Shader.SetGlobalTexture(
                    string.Format("_VTTiledTex{0}", i),
                    Textures[i]);
            }

            // Set global shader parameters for tile mapping:
            // x: Padding offset as a fraction of tile size with padding
            // y: Effective tile size as a fraction of tile size with padding
            // zw: Reciprocal of region size (for fast division in shaders)
            Shader.SetGlobalVector(
                "_VTTileParam", 
                new Vector4(
                    (float)PaddingSize / TileSizeWithPadding,
                    (float)TileSize / TileSizeWithPadding,
                    1.0f / RegionSize.x,
                    1.0f / RegionSize.y));
        }

        private void Update()
        {
            Stat.Reset();
        }

        /// <summary>
        /// Requests allocation of a tile in the physical texture.
        /// Returns the least recently used tile for reuse.
        /// </summary>
        /// <returns>The coordinates of the allocated tile</returns>
        public Vector2Int RequestTile()
        {
            return IdToPos(m_TilePool.First);
        }

        /// <summary>
        /// Marks a tile as active, updating its position in the LRU cache.
        /// </summary>
        /// <param name="tile">Coordinates of the tile to mark as active</param>
        /// <returns>True if the tile was successfully marked as active</returns>
        public bool SetActive(Vector2Int tile)
        {
            bool success = m_TilePool.SetActive(PosToId(tile));

            if (success)
                Stat.AddActive(PosToId(tile));

            return success;
        }

        /// <summary>
        /// Updates the content of a tile with new texture data.
        /// </summary>
        /// <param name="tile">Coordinates of the tile to update</param>
        /// <param name="textures">Array of textures for each layer to copy into the tile</param>
        public void UpdateTile(Vector2Int tile, Texture2D[] textures)
        {
            if (!SetActive(tile))
                return;

            if(textures == null)
                return;

            // Update each layer with the corresponding texture
            for(int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null)
                {
                    DrawTexture(
                        textures[i], 
                        Textures[i], 
                        new RectInt(
                            tile.x * TileSizeWithPadding, 
                            tile.y * TileSizeWithPadding, 
                            TileSizeWithPadding, 
                            TileSizeWithPadding));
                }
            }

            // Notify subscribers that the tile has been updated
            OnTileUpdateComplete?.Invoke(tile);
        }

        /// <summary>
        /// Converts a linear tile ID to 2D tile coordinates.
        /// </summary>
        /// <param name="id">Linear tile ID</param>
        /// <returns>2D tile coordinates (x, y)</returns>
        private Vector2Int IdToPos(int id)
        {
            return new Vector2Int(id % RegionSize.x, id / RegionSize.x);
        }

        /// <summary>
        /// Converts 2D tile coordinates to a linear tile ID.
        /// </summary>
        /// <param name="tile">2D tile coordinates</param>
        /// <returns>Linear tile ID</returns>
        private int PosToId(Vector2Int tile)
        {
            return (tile.y * RegionSize.x + tile.x);
        }

        /// <summary>
        /// Draws a source texture into a specific region of a target render texture.
        /// </summary>
        /// <param name="source">The source texture to draw</param>
        /// <param name="target">The target render texture</param>
        /// <param name="position">The position and size in the target to draw to</param>
        private void DrawTexture(Texture source, RenderTexture target, RectInt position)
        {
            if (source == null || target == null || m_DrawTextureShader == null)
                return;

            // Create the drawing material if it doesn't exist
            if (m_DrawTextureMateral == null)
                m_DrawTextureMateral = new Material(m_DrawTextureShader);

            // Calculate transformation matrix to position the texture correctly
            // Convert from pixel coordinates to the (-1,1) normalized device coordinate space
            float l = position.x * 2.0f / target.width - 1;
            float r = (position.x + position.width) * 2.0f / target.width - 1;
            float b = position.y * 2.0f / target.height - 1;
            float t = (position.y + position.height) * 2.0f / target.height - 1;
            
            var mat = new Matrix4x4();
            mat.m00 = r - l;
            mat.m03 = l;
            mat.m11 = t - b;
            mat.m13 = b;
            mat.m23 = -1;
            mat.m33 = 1;

            // Set the transformation matrix and draw the texture
            m_DrawTextureMateral.SetMatrix(Shader.PropertyToID("_ImageMVP"), GL.GetGPUProjectionMatrix(mat, true));

            target.DiscardContents();
            Graphics.Blit(source, target, m_DrawTextureMateral);
        }
    }
}
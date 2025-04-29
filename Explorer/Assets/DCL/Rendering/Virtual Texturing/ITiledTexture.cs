using System;
using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Interface for components that manage physical tiled textures.
    /// 
    /// The tiled texture is a physical texture in memory that stores the actual
    /// texture data for virtual texture pages. It's divided into a grid of tiles
    /// that can be dynamically updated as different parts of the virtual texture
    /// are requested.
    /// </summary>
    public interface ITiledTexture
    {
        /// <summary>
        /// Event fired when a tile's content has been updated.
        /// 
        /// This event allows other components to know when a tile's data has been
        /// completely updated, so they can update their own state accordingly.
        /// </summary>
        event Action<Vector2Int> OnTileUpdateComplete;

        /// <summary>
        /// The dimensions of the tiled texture in number of tiles.
        /// 
        /// For example, a RegionSize of (4, 4) means the physical texture is divided
        /// into a 4×4 grid of tiles, for a total of 16 tiles.
        /// </summary>
        Vector2Int RegionSize { get; }

        /// <summary>
        /// The size of each tile in pixels (excluding padding).
        /// 
        /// Each tile is a square with this width and height. This defines the usable
        /// area of the tile, not including padding pixels.
        /// </summary>
        int TileSize { get; }

        /// <summary>
        /// The number of padding pixels around each tile.
        /// 
        /// Padding helps prevent filtering artifacts at tile boundaries by providing
        /// additional pixels beyond the tile's edge that can be sampled.
        /// </summary>
        int PaddingSize { get; }

        /// <summary>
        /// The number of texture layers managed by this component.
        /// 
        /// Multiple layers allow the system to support different texture types
        /// (diffuse, normal, specular, etc.) with the same virtual texture coordinates.
        /// </summary>
        int LayerCount { get; }

        /// <summary>
        /// Requests allocation of a tile in the physical texture.
        /// 
        /// This typically uses a least-recently-used (LRU) policy to reclaim tiles
        /// that haven't been used recently when the physical texture is full.
        /// </summary>
        /// <returns>The coordinates of the allocated tile</returns>
        Vector2Int RequestTile();

        /// <summary>
        /// Marks a tile as active, updating its position in the LRU cache.
        /// </summary>
        /// <param name="tile">Coordinates of the tile to mark as active</param>
        /// <returns>True if the tile was successfully marked as active</returns>
        bool SetActive(Vector2Int tile);

        /// <summary>
        /// Updates the content of a tile with new texture data.
        /// </summary>
        /// <param name="tile">Coordinates of the tile to update</param>
        /// <param name="textures">Array of textures for each layer to copy into the tile</param>
        void UpdateTile(Vector2Int tile, Texture2D[] textures);
    }
}
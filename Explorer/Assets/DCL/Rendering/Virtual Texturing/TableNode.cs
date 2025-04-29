using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Represents a node in the quadtree structure of the virtual texture page table.
    /// 
    /// TableNode implements a hierarchical structure where each node represents a region
    /// of the virtual texture at a specific mipmap level. The hierarchy allows efficient
    /// lookup of available texture data at various levels of detail.
    /// </summary>
    public class TableNode
    {
        /// <summary>
        /// Child nodes in the quadtree (null until lazy-initialized when needed)
        /// </summary>
        private TableNode[] m_Children;

        /// <summary>
        /// The mipmap level this node represents (0 is most detailed)
        /// </summary>
        public int MipLevel { get; }
        
        /// <summary>
        /// The rectangle in the page table covered by this node
        /// </summary>
        public RectInt Rect { get; }

        /// <summary>
        /// The payload data associated with this node (texture coordinates, state, etc.)
        /// </summary>
        public PagePayload Payload { get; } 

        /// <summary>
        /// Creates a new table node representing a specific region of the page table.
        /// </summary>
        /// <param name="mip">Mipmap level for this node</param>
        /// <param name="x">X-coordinate in the page table</param>
        /// <param name="y">Y-coordinate in the page table</param>
        /// <param name="width">Width of the region in the page table</param>
        /// <param name="height">Height of the region in the page table</param>
        public TableNode(int mip, int x, int y, int width, int height)
        {
            MipLevel = mip;
            Rect = new RectInt(x, y, width, height);
            Payload = new PagePayload();
        }

        /// <summary>
        /// Gets a node with exact coordinates and mipmap level, if it exists.
        /// </summary>
        /// <param name="x">X-coordinate to search for</param>
        /// <param name="y">Y-coordinate to search for</param>
        /// <param name="mip">Mipmap level to search for</param>
        /// <returns>
        /// The matching node if found; null otherwise
        /// </returns>
        public TableNode Get(int x, int y, int mip)
        {
            if (!Contains(x, y))
                return null;

            if (MipLevel == mip)
                return this;

            if (m_Children != null)
            {
                foreach(var child in m_Children)
                {
                    var item = child.Get(x, y, mip);
                    if (item != null)
                        return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the most detailed available node that covers the specified coordinates.
        /// Only returns nodes that are ready (have texture data loaded).
        /// </summary>
        /// <param name="x">X-coordinate to search for</param>
        /// <param name="y">Y-coordinate to search for</param>
        /// <param name="mip">Target mipmap level (will return a node at this level or higher)</param>
        /// <returns>
        /// The most appropriate available node, or null if no suitable node exists
        /// </returns>
        public TableNode GetAvailable(int x, int y, int mip)
        {
            if (!Contains(x, y))
                return null;

            if (MipLevel > mip && m_Children != null)
            {
                foreach (var child in m_Children)
                {
                    var item = child.GetAvailable(x, y, mip);
                    if (item != null)
                        return item;
                }
            }

            return (Payload.IsReady ? this : null);
        }

        /// <summary>
        /// Gets or creates the child node containing the specified coordinates.
        /// Lazily initializes the quadtree structure when needed.
        /// </summary>
        /// <param name="x">X-coordinate to look up</param>
        /// <param name="y">Y-coordinate to look up</param>
        /// <returns>
        /// The child node containing the coordinates, or null if this is a leaf node
        /// </returns>
        public TableNode GetChild(int x, int y)
        {
            if (!Contains(x, y))
                return null;

            if (MipLevel == 0)
                return null;

            // Lazy initialization of children
            if (m_Children == null)
            {
                m_Children = new TableNode[4];
                // Create four child nodes in a quadtree arrangement
                m_Children[0] = new TableNode(MipLevel - 1, Rect.x, Rect.y, Rect.width / 2, Rect.height / 2);
                m_Children[1] = new TableNode(MipLevel - 1, Rect.x + Rect.width / 2, Rect.y, Rect.width / 2, Rect.height / 2);
                m_Children[2] = new TableNode(MipLevel - 1, Rect.x, Rect.y + Rect.height / 2, Rect.width / 2, Rect.height / 2);
                m_Children[3] = new TableNode(MipLevel - 1, Rect.x + Rect.width / 2, Rect.y + Rect.height / 2, Rect.width / 2, Rect.height / 2);
            }

            foreach (var child in m_Children)
            {
                if (child.Contains(x, y))
                    return child;
            }

            return null;
        }

        /// <summary>
        /// Checks if this node's rectangle contains the specified coordinates.
        /// </summary>
        /// <param name="x">X-coordinate to check</param>
        /// <param name="y">Y-coordinate to check</param>
        /// <returns>True if the coordinates are within this node's rectangle</returns>
        private bool Contains(int x, int y)
        {
            if (x < Rect.x || x >= Rect.xMax)
                return false;

            if (y < Rect.y || y >= Rect.yMax)
                return false;

            return true;
        }
    }
}
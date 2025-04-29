using System.Collections.Generic;
using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Statistics collector for the TiledTexture component.
    /// 
    /// Tracks metrics about physical texture usage, including how many tiles
    /// are active and the maximum number of active tiles observed.
    /// </summary>
    public class TileTextureStat
    {
        /// <summary>
        /// Dictionary tracking active tiles to prevent double-counting
        /// </summary>
        private Dictionary<int, int> m_Map = new Dictionary<int, int>();

        /// <summary>
        /// Number of tiles currently active in the physical texture
        /// </summary>
        public int CurrentActive { get; private set; }
        
        /// <summary>
        /// Maximum number of tiles that have been active simultaneously
        /// </summary>
        public int MaxActive { get; private set; }

        /// <summary>
        /// Resets statistics for a new frame.
        /// Updates MaxActive with the peak value from the previous frame.
        /// </summary>
        public void Reset()
        {
            MaxActive = Mathf.Max(MaxActive, CurrentActive);
            CurrentActive = 0;
            m_Map.Clear();
        }

        /// <summary>
        /// Records that a tile has been activated.
        /// Ensures each tile is only counted once per frame.
        /// </summary>
        /// <param name="id">ID of the activated tile</param>
        public void AddActive(int id)
        {
            if(!m_Map.ContainsKey(id))
            {
                m_Map[id] = 1;
                CurrentActive++;
            }
        }
    }
}
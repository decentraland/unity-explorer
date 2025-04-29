using System.Collections.Generic;

namespace VirtualTexture
{
    /// <summary>
    /// Implements a Least Recently Used (LRU) cache for tile management.
    /// 
    /// The LRU cache tracks which tiles have been used recently, allowing the
    /// system to identify which tiles should be reclaimed first when new space
    /// is needed. Tiles that haven't been accessed recently are considered good
    /// candidates for replacement.
    /// </summary>
    public class LruCache
    {
        /// <summary>
        /// Dictionary for O(1) lookup of nodes by ID
        /// </summary>
        private Dictionary<int, LinkedListNode<int>> m_Map = new Dictionary<int, LinkedListNode<int>>();
        
        /// <summary>
        /// Linked list maintaining the order of use (least recently used at the front)
        /// </summary>
        private LinkedList<int> m_List = new LinkedList<int>();

        /// <summary>
        /// Gets the ID of the least recently used item.
        /// This is the item that should be replaced first when new space is needed.
        /// </summary>
        public int First { get { return m_List.First.Value; } }

        /// <summary>
        /// Adds a new item to the cache.
        /// New items are added at the end of the list (most recently used position).
        /// </summary>
        /// <param name="id">ID of the item to add</param>
        public void Add(int id)
        {
            if (m_Map.ContainsKey(id))
                return;

            var node = new LinkedListNode<int>(id);
            m_Map.Add(id, node);
            m_List.AddLast(node);
        }

        /// <summary>
        /// Marks an item as recently used, moving it to the end of the list.
        /// </summary>
        /// <param name="id">ID of the item to mark as active</param>
        /// <returns>True if the item was found and marked as active, false otherwise</returns>
        public bool SetActive(int id)
        {
            LinkedListNode<int> node = null;
            if (!m_Map.TryGetValue(id, out node))
                return false;

            // Move the node to the end of the list (most recently used position)
            m_List.Remove(node);
            m_List.AddLast(node);

            return true;
        }
    }
}
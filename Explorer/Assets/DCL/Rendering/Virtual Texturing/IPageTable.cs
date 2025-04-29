namespace VirtualTexture
{
    /// <summary>
    /// Interface for the virtual texture page table component.
    /// 
    /// The page table maintains mappings between virtual texture coordinates and
    /// physical texture locations, and tracks which pages are loaded at which
    /// mipmap levels.
    /// </summary>
    public interface IPageTable
    {
        /// <summary>
        /// Size of the page table in each dimension.
        /// 
        /// This value determines how many virtual texture pages can be addressed
        /// in each dimension. The total number of addressable pages is TableSize².
        /// </summary>
        int TableSize { get; }

        /// <summary>
        /// Maximum supported mipmap level in the page table.
        /// 
        /// This value determines the range of detail levels available in the
        /// virtual texturing system. Level 0 is the most detailed, with each
        /// level up reducing resolution by a factor of 2 in each dimension.
        /// </summary>
        int MaxMipLevel { get; }
    }
}
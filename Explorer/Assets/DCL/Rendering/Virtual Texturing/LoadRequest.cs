namespace VirtualTexture
{
    /// <summary>
    /// Represents a request to load a specific virtual texture page.
    /// 
    /// LoadRequest objects store the coordinates and mipmap level of the virtual texture
    /// page being requested, allowing the system to track and manage multiple 
    /// concurrent load operations.
    /// </summary>
    public class LoadRequest
    {
        /// <summary>
        /// X-coordinate of the page in the virtual texture page table.
        /// </summary>
        public int PageX { get; }

        /// <summary>
        /// Y-coordinate of the page in the virtual texture page table.
        /// </summary>
        public int PageY { get; }

        /// <summary>
        /// Mipmap level of the page to be loaded.
        /// Higher values represent lower resolution versions of the texture.
        /// </summary>
        public int MipLevel { get; }

        /// <summary>
        /// Creates a new load request for a specific virtual texture page.
        /// </summary>
        /// <param name="x">X-coordinate in the page table</param>
        /// <param name="y">Y-coordinate in the page table</param>
        /// <param name="mip">Mipmap level to load</param>
        public LoadRequest(int x, int y, int mip)
        {
            PageX = x;
            PageY = y;
            MipLevel = mip;
        }
    }
}
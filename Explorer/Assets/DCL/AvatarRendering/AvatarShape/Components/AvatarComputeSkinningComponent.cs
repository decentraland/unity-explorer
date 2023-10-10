using DCL.AvatarRendering.AvatarShape.ComputeShader;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Stores data for the compute shader to perform skinning
    ///     TODO move more parts here
    /// </summary>
    public struct AvatarComputeSkinningComponent
    {
        /// <summary>
        ///     Acquired Region of the common buffer
        /// </summary>
        public FixedComputeBufferHandler.Slice VertsOutRegion;
    }
}

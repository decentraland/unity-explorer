namespace DCL.Profiling
{
    public enum CrdtTrafficDirection : byte
    {
        /// <summary>
        ///     Messages the scene's JS runtime sent to the renderer.
        /// </summary>
        FromScene = 0,

        /// <summary>
        ///     Messages the renderer sent back to the scene's JS runtime.
        /// </summary>
        ToScene = 1,
    }
}

using System;
using System.Threading;

namespace CrdtEcsBridge.WorldSynchronizer
{
    /// <summary>
    ///     Synchronizes the state of the world accordingly to the given instructions
    /// </summary>
    public interface ICRDTWorldSynchronizer : IDisposable
    {
        /// <summary>
        ///     Get the command buffer to fill it with the CRDT messages
        ///     Only one buffer can be rented at a time.
        ///     Can be called from the background thread
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">If the previous command buffer was not finalized the exception will be thrown</exception>
        IWorldSyncCommandBuffer GetSyncCommandBuffer();

        /// <summary>
        ///     Should be called from the main thread to apply the changes to the ECS World
        ///     Finalizes the command buffer and allows to rent it again.
        /// </summary>
        /// <param name="syncCommandBuffer"></param>
        void ApplySyncCommandBuffer(IWorldSyncCommandBuffer syncCommandBuffer);

        /// <summary>
        ///     Disposes a rented command buffer that will never be applied and frees the rent slot.
        ///     Every buffer obtained from <see cref="GetSyncCommandBuffer" /> must reach exactly one of
        ///     <see cref="ApplySyncCommandBuffer" /> or this method, otherwise the single rent slot leaks
        ///     and all subsequent rents time out.
        ///     Can be called from the background thread
        /// </summary>
        /// <param name="syncCommandBuffer"></param>
        void AbortSyncCommandBuffer(IWorldSyncCommandBuffer syncCommandBuffer);
    }
}

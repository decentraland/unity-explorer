using System;

namespace CrdtEcsBridge.WorldSynchronizer
{
    /// <summary>
    /// Synchronizes the state of the world accordingly to the given instructions
    /// </summary>
    public interface ICrdtWorldSynchronizer
    {
        /// <summary>
        /// Get the command buffer to fill it with the CRDT messages
        /// Only one buffer can be rented at a time
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">If the previous command buffer was not finalized the exception will be thrown</exception>
        WorldSyncCommandBuffer GetSyncCommandBuffer();

        /// <summary>
        /// Should be called from the main thread to apply the changes to the ECS World
        /// Finalizes the command buffer and allows to rent it again
        /// </summary>
        /// <param name="syncCommandBuffer"></param>
        void ApplySyncCommandBuffer(WorldSyncCommandBuffer syncCommandBuffer);
    }
}

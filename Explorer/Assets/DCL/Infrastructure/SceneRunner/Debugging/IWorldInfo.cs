using System.Collections.Generic;

namespace SceneRunner.Debugging
{
    public interface IWorldInfo
    {
        string EntityComponentsInfo(int entityId);

        IReadOnlyList<int> EntityIds();

        /// <summary>
        ///     Resolves the entity <paramref name="entityId" /> names into <paramref name="crdtEntityId" />, the id the
        ///     scene's own code and the scene-room traffic address entities by, unlike <paramref name="entityId" />,
        ///     which is the index of the entity in the ECS world.
        /// </summary>
        /// <returns>False when the entity does not exist or is not backed by a CRDT entity.</returns>
        bool TryGetCrdtEntityId(int entityId, out int crdtEntityId);
    }
}

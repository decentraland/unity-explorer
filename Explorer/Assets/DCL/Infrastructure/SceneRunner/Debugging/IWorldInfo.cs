using System.Collections.Generic;

namespace SceneRunner.Debugging
{
    public interface IWorldInfo
    {
        string EntityComponentsInfo(int entityId);

        IReadOnlyList<int> EntityIds();

        /// <summary>
        ///     The CRDT id of the entity <paramref name="entityId" /> names. That is the id the scene's own code and
        ///     the scene-room traffic address entities by, unlike <paramref name="entityId" />, which is the index of
        ///     the entity in the ECS world.
        /// </summary>
        /// <returns>Null when the entity does not exist or is not backed by a CRDT entity.</returns>
        int? CrdtEntityId(int entityId);
    }
}

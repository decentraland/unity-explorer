using Arch.Core;
using CRDT;

namespace CrdtEcsBridge.Components
{
    /// <summary>
    ///     Create an ECS entity from CRDTEntity called from the scene
    /// </summary>
    public interface ISceneEntityFactory
    {
        public Entity Create(CRDTEntity crdtEntity, World world);
    }
}

using Arch.Core;
using CRDT;

namespace CrdtEcsBridge.Components
{
    /// <summary>
    /// Create an ECS entity from CRDTEntity
    /// </summary>
    public interface IEntityFactory
    {
        public Entity Create(CRDTEntity crdtEntity, World world);
    }
}

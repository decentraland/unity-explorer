using Arch.Core;

namespace DCL.MapPins.Components
{
    public struct MapPinHolderComponent
    {
        public Entity GlobalWorldEntity;

        public MapPinHolderComponent(Entity globalWorldEntity)
        {
            GlobalWorldEntity = globalWorldEntity;
        }
    }
}

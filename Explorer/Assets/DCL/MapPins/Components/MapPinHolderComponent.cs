using Arch.Core;

namespace DCL.MapPins.Components
{
    public struct MapPinHolderComponent
    {
        public Entity GlobalWorldEntity;
        public bool HasTexturePromise;

        public MapPinHolderComponent(Entity globalWorldEntity, bool hasTexturePromise)
        {
            GlobalWorldEntity = globalWorldEntity;
            HasTexturePromise = hasTexturePromise;
        }
    }
}

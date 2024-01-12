using Arch.Core;

namespace ECS.Unity.AvatarShape.Components
{
    public struct SDKAvatarShapeComponent
    {
        public Entity globalWorldEntity;

        public SDKAvatarShapeComponent(Entity globalWorldEntity)
        {
            this.globalWorldEntity = globalWorldEntity;
        }
    }
}

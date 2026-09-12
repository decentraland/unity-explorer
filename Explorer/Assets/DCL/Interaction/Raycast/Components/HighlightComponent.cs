using Arch.Core;
using System.Runtime.CompilerServices;

namespace DCL.Interaction.Raycast.Components
{
    public struct HighlightComponent
    {
        private bool isEnabled;
        private Entity currentEntity;
        private Entity nextEntity;

        public HighlightComponent(bool isEnabled, Entity currentEntity, Entity nextEntity) : this()
        {
            this.isEnabled = isEnabled;
            this.currentEntity = currentEntity;
            this.nextEntity = nextEntity;
        }

        public static HighlightComponent NewEntityHighlightComponent(Entity entityRef) =>
            new (
                true,
                entityRef,
                entityRef
            );

        public void Setup(Entity newNextEntity)
        {
            isEnabled = true;
            nextEntity = newNextEntity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            isEnabled = false;
            nextEntity = Entity.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Disable()
        {
            nextEntity = Entity.Null;
            isEnabled = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveNextAndRemoveMaterial()
        {
            currentEntity = nextEntity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Entity CurrentEntityOrNull() =>
            currentEntity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsEmpty() =>
            currentEntity == Entity.Null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool CanPassAnUpdate() =>
            currentEntity == nextEntity && isEnabled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ReadyForMaterial(World world) =>
            isEnabled && nextEntity != Entity.Null && world.IsAlive(nextEntity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SwitchEntity()
        {
            currentEntity = nextEntity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasToResetLastEntity(World world) =>
            isEnabled && currentEntity != nextEntity && currentEntity != Entity.Null && world.IsAlive(currentEntity);
    }
}

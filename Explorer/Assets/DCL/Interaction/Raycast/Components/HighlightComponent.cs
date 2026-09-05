using Arch.Core;
using DCL.Utilities.Extensions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Interaction.Raycast.Components
{
    public struct HighlightComponent
    {
        private bool isEnabled;
        private bool isAtDistance;
        private Entity currentEntity;
        private Entity nextEntity;

        public HighlightComponent(bool isEnabled, bool isAtDistance, Entity currentEntity, Entity nextEntity) : this()
        {
            this.isEnabled = isEnabled;
            this.isAtDistance = isAtDistance;
            this.currentEntity = currentEntity;
            this.nextEntity = nextEntity;
        }

        public static HighlightComponent NewEntityHighlightComponent(bool isAtDistance, Entity entityRef) =>
            new (
                true,
                isAtDistance,
                entityRef,
                entityRef
            );

        public void Setup(bool atDistance, Entity newNextEntity)
        {
            isEnabled = true;
            isAtDistance = atDistance;
            nextEntity = newNextEntity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            isEnabled = false;
            isAtDistance = false;
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
        public readonly bool IsAtDistance() =>
            isAtDistance;

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

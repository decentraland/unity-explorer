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
        private EntityReference currentEntity;
        private EntityReference nextEntity;
        private Material? materialOnUse;

        //Should be repooled
        public readonly Dictionary<Renderer, Material[]> OriginalMaterials;

        public HighlightComponent(bool isEnabled, bool isAtDistance, EntityReference currentEntity, EntityReference nextEntity) : this()
        {
            this.isEnabled = isEnabled;
            this.isAtDistance = isAtDistance;
            this.currentEntity = currentEntity;
            this.nextEntity = nextEntity;
            OriginalMaterials = new Dictionary<Renderer, Material[]>();
        }

        public void Setup(bool atDistance, EntityReference newNextEntity)
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
            nextEntity = EntityReference.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Disable()
        {
            nextEntity = EntityReference.Null;
            isEnabled = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveNextAndRemoveMaterial()
        {
            materialOnUse = null;
            currentEntity = nextEntity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Material MaterialOnUse() =>
            materialOnUse.EnsureNotNull();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsAtDistance() =>
            isAtDistance;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly EntityReference CurrentEntityOrNull() =>
            currentEntity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsEmpty() =>
            currentEntity == EntityReference.Null || materialOnUse == null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool CanPassAnUpdate(Material materialToUse) =>
            materialOnUse == materialToUse
            && currentEntity == nextEntity
            && isEnabled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ReadyForMaterial() =>
            materialOnUse == null && isEnabled && nextEntity != EntityReference.Null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateMaterialAndSwitchEntity(Material materialToUse)
        {
            currentEntity = nextEntity;
            materialOnUse = materialToUse;
        }
    }
}

using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class PlayerSpatialStateNetSendSystem : BaseUnityLoopSystem
    {
        private readonly CharacterController playerCharacter;
        private readonly Entity playerEntity;

        public PlayerSpatialStateNetSendSystem(World world, CharacterController playerCharacter, Entity playerEntity) : base(world)
        {
            this.playerCharacter = playerCharacter;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {


        }
    }
}

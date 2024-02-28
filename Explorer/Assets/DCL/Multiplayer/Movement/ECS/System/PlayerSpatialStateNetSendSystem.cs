using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class PlayerSpatialStateNetSendSystem : BaseUnityLoopSystem
    {
        private readonly CharacterController playerCharacter;
        private readonly Entity playerEntity;

        private readonly IMultiplayerSpatialStateSettings settings;


        public PlayerSpatialStateNetSendSystem(World world, IMultiplayerSpatialStateSettings settings) : base(world)
        {
            this.settings = settings;
        }

        // public PlayerSpatialStateNetSendSystem(World world, CharacterController playerCharacter, Entity playerEntity) : base(world)
        // {
        //     this.playerCharacter = playerCharacter;
        //     this.playerEntity = playerEntity;
        // }

        protected override void Update(float t)
        {
            Debug.Log($"VVV {settings.Latency}");
        }
    }
}

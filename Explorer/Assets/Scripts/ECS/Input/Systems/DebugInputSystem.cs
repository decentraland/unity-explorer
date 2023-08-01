using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Input.Component;
using System;
using UnityEngine;

namespace ECS.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]

    public partial class DebugInputSystem : BaseUnityLoopSystem
    {
        // Start is called before the first frame update
        public DebugInputSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            //DebugCQuery(World);
            //DebugDQuery(World);
        }

        [Query]
        private void DebugC(ref CameraZoomComponent cameraZoomComponent)
        {
            Debug.Log("Camera: " + cameraZoomComponent.DoZoomIn);
        }

        [Query]
        private void DebugD(ref PrimaryKey primaryKey)
        {
            Debug.Log("Primary: " + primaryKey.IsKeyDown());
        }

    }
}




using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Components;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(ConcludeTextureLoadingSystem))]
    public partial class RepeatTextureLoadingSystem : RepeatLoadingSystemBase<GetTextureIntention, Texture2D>
    {
        public RepeatTextureLoadingSystem(World world) : base(world) { }

        protected override UnityWebRequest CreateWebRequest(in GetTextureIntention intention) =>
            StartLoadingTextureSystem.GetTextureRequest(in intention);
    }
}

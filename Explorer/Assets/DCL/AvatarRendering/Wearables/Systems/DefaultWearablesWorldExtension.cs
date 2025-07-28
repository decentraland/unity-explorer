using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using ECS.Abstract;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    public static class DefaultWearablesWorldExtension
    {
        private static readonly QueryDescription QUERY = new QueryDescription().WithAll<DefaultWearablesComponent>();

        public static SingleInstanceEntity CacheDefaultWearablesState(this World world) =>
            new (in QUERY, world);

        public static ref readonly DefaultWearablesComponent GetDefaultWearablesState(this in SingleInstanceEntity instance, World world) =>
            ref world.Get<DefaultWearablesComponent>(instance);
    }
}

using Arch.Core;
using DCL.Character.Components;
using DCL.Profiles;

namespace Global.Dynamic
{
    public static class DynamicECSWorldExtensions
    {
        private static readonly Entity[] OWN_PROFILE_BUFFER = new Entity[1];

        public static void SetProfileToOwnPlayer(this World world, Profile profile)
        {
            world.GetEntities(in new QueryDescription().WithAll<PlayerComponent>(), OWN_PROFILE_BUFFER);
            Entity ownPlayerEntity = OWN_PROFILE_BUFFER[0];

            if (world.Has<Profile>(ownPlayerEntity))
                world.Set(ownPlayerEntity, profile);
            else
                world.Add(ownPlayerEntity, profile);
        }
    }
}

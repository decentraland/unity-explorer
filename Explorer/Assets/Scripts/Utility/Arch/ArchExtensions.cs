﻿using Arch.Core;

namespace Utility.Arch
{
    public static class ArchExtensions
    {
        public static bool TryRemove<T>(this World world, Entity entity)
        {
            if (!world.Has<T>(entity)) return false;

            world.Remove<T>(entity);
            return true;
        }

        public static void AddOrSet<T>(this World world, Entity entity, T component)
        {
            ref var existingComponent = ref world.AddOrGet(entity, component);
            existingComponent = component;
        }
    }
}

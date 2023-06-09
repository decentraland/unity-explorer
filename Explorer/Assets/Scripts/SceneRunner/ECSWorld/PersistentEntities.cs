﻿using Arch.Core;

namespace SceneRunner.ECSWorld
{
    /// <summary>
    ///     Entities that are created in a world factory and never destroyed while the ECS World is alive
    /// </summary>
    public readonly struct PersistentEntities
    {
        public readonly EntityReference SceneRoot;

        public PersistentEntities(EntityReference sceneRoot)
        {
            SceneRoot = sceneRoot;
        }
    }
}

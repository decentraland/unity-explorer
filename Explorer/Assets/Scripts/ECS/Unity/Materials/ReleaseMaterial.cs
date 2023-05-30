using Arch.Core;
using ECS.StreamableLoading.Components.Common;
using ECS.Unity.Materials.Components;

namespace ECS.Unity.Materials
{
    /// <summary>
    ///     Executes the logic to clean-up material
    /// </summary>
    public static class ReleaseMaterial
    {
        public static void Execute(World world, ref MaterialComponent materialComponent, IMaterialsCache materialsCache)
        {
            switch (materialComponent.Status)
            {
                // Dereference the loaded material
                case MaterialComponent.LifeCycle.LoadingFinished or MaterialComponent.LifeCycle.MaterialApplied when materialComponent.Result:
                    materialsCache.Dereference(in materialComponent.Data);
                    break;

                // Abort the loading process of the textures
                case MaterialComponent.LifeCycle.LoadingInProgress:
                    TryAddAbortIntention(world, ref materialComponent.AlbedoTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.EmissiveTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.AlphaTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.BumpTexPromise);
                    break;
            }

            materialComponent.Status = MaterialComponent.LifeCycle.LoadingNotStarted;
        }

        public static void Execute(World world, ref MaterialComponent materialComponent, DestroyMaterial destroyMaterial)
        {
            switch (materialComponent.Status)
            {
                // Dereference the loaded material
                case MaterialComponent.LifeCycle.LoadingFinished or MaterialComponent.LifeCycle.MaterialApplied when materialComponent.Result:
                    destroyMaterial(in materialComponent.Data, materialComponent.Result);
                    break;

                // Abort the loading process of the textures
                case MaterialComponent.LifeCycle.LoadingInProgress:
                    TryAddAbortIntention(world, ref materialComponent.AlbedoTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.EmissiveTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.AlphaTexPromise);
                    TryAddAbortIntention(world, ref materialComponent.BumpTexPromise);
                    break;
            }
        }

        private static void TryAddAbortIntention(World world, ref EntityReference entityReference)
        {
            if (!entityReference.IsAlive(world)) return;

            world.Add(entityReference.Entity, new ForgetLoadingIntent());

            // Nullify the entity reference
            entityReference = EntityReference.Null;
        }
    }
}

using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using UnityEngine;
using Utility;

namespace ECS.Unity.Transforms
{
    public static class ExposedTransformUtils
    {
        /// <summary>
        ///     It's sufficient to store one instance only as we are going to override it every time we write the data
        /// </summary>
        private static readonly SDKTransform TRANSFORM_SHARED = new ();

        public static void Put(IECSToCRDTWriter ecsToCrdtWriter, IExposedTransform exposedTransform, CRDTEntity entity, Vector3 scenePosition, bool checkIsDirty)
        {
            if (checkIsDirty && !exposedTransform.Position.IsDirty && !exposedTransform.Rotation.IsDirty)
                return;

            TRANSFORM_SHARED.Position = ParcelMathHelper.GetSceneRelativePosition(exposedTransform.Position.Value, scenePosition);
            TRANSFORM_SHARED.Rotation = exposedTransform.Rotation.Value;

            ecsToCrdtWriter.PutMessage(entity, TRANSFORM_SHARED);
        }
    }
}

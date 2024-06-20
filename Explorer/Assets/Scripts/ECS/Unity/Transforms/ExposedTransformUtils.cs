using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using UnityEngine;
using Utility;

namespace ECS.Unity.Transforms
{
    public static class ExposedTransformUtils
    {
        public static SDKTransform? Put(IECSToCRDTWriter ecsToCrdtWriter, IExposedTransform exposedTransform, CRDTEntity entity, Vector3 scenePosition, bool checkIsDirty)
        {
            if (checkIsDirty && !exposedTransform.Position.IsDirty && !exposedTransform.Rotation.IsDirty)
                return null;

            return ecsToCrdtWriter.PutMessage<SDKTransform, (IExposedTransform, Vector3)>(static (c, data) =>
            {
                c.Position = ParcelMathHelper.GetSceneRelativePosition(data.Item1.Position.Value, data.Item2);
                c.Rotation = data.Item1.Rotation.Value;
            }, entity, (exposedTransform, scenePosition));
        }
    }
}

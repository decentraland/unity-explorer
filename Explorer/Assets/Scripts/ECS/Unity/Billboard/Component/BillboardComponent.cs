using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.Billboard.Component
{
    public struct BillboardComponent
    {
        public bool BlockX;
        public bool BlockY;
        public bool BlockZ;

        public BillboardComponent(PBBillboard pbBillboard)
        {
            var mode = pbBillboard.BillboardMode;
            BlockX = mode.HasFlag(BillboardMode.BmX);
            BlockY = mode.HasFlag(BillboardMode.BmY);
            BlockZ = mode.HasFlag(BillboardMode.BmZ);
        }

        public void Apply(PBBillboard pbBillboard)
        {
            var mode = pbBillboard.BillboardMode;
            BlockX = mode.HasFlag(BillboardMode.BmX);
            BlockY = mode.HasFlag(BillboardMode.BmY);
            BlockZ = mode.HasFlag(BillboardMode.BmZ);
        }

        public Vector3 AsVector3()
        {
            static float AsFloat(bool value)
            {
                return value ? 1 : 0;
            }

            return new Vector3(
                AsFloat(BlockX),
                AsFloat(BlockY),
                AsFloat(BlockZ)
            );
        }
    }
}

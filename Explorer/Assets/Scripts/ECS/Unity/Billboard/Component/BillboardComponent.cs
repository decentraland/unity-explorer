using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.Billboard.Component
{
    public struct BillboardComponent
    {
        public bool UseX;
        public bool UseY;
        public bool UseZ;

        public BillboardComponent(PBBillboard pbBillboard) : this(
            pbBillboard.BillboardMode.HasFlag(BillboardMode.BmX),
            pbBillboard.BillboardMode.HasFlag(BillboardMode.BmY),
            pbBillboard.BillboardMode.HasFlag(BillboardMode.BmZ)
        ) { }

        public BillboardComponent(bool useX, bool useY, bool useZ)
        {
            UseX = useX;
            UseY = useY;
            UseZ = useZ;
        }

        public void Apply(PBBillboard pbBillboard)
        {
            var mode = pbBillboard.BillboardMode;
            UseX = mode.HasFlag(BillboardMode.BmX);
            UseY = mode.HasFlag(BillboardMode.BmY);
            UseZ = mode.HasFlag(BillboardMode.BmZ);
        }

        public Vector3 AsVector3()
        {
            static float AsFloat(bool value)
            {
                return value ? 1 : 0;
            }

            return new Vector3(
                AsFloat(UseX),
                AsFloat(UseY),
                AsFloat(UseZ)
            );
        }

        public override string ToString()
        {
            return $"Billboard: {{x: {UseX}; y: {UseY}; z: {UseZ}}}";
        }
    }
}

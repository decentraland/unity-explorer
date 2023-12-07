using DCL.ECSComponents;

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
    }
}

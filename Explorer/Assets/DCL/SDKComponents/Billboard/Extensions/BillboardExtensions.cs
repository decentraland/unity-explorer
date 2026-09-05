using DCL.ECSComponents;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Utility;

namespace DCL.Billboard.Extensions
{
    public static class BillboardExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseX(this PBBillboard billboard) =>
            EnumUtils.HasFlag(billboard.BillboardMode, BillboardMode.BmX);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseY(this PBBillboard billboard) =>
            EnumUtils.HasFlag(billboard.BillboardMode, BillboardMode.BmY);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseZ(this PBBillboard billboard) =>
            EnumUtils.HasFlag(billboard.BillboardMode, BillboardMode.BmZ);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SuppressMessage("ReSharper", "BitwiseOperatorOnEnumWithoutFlags")]
        public static void Apply(this PBBillboard billboard, bool useX, bool useY, bool useZ)
        {
            var mode = BillboardMode.BmNone;

            if (useX) mode |= BillboardMode.BmX;
            if (useY) mode |= BillboardMode.BmY;
            if (useZ) mode |= BillboardMode.BmZ;

            billboard.BillboardMode = mode;
        }
        
        public static BillboardMode GetBillboardMode(this PBBillboard self)
        {
            return self.HasBillboardMode ? self.BillboardMode : BillboardMode.BmAll;
        }

        public static string AsString(this PBBillboard billboard) =>
            $"Billboard: {{x: {billboard.UseX()}; y: {billboard.UseY()}; z: {billboard.UseZ()}}}";
    }
}

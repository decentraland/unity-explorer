using DCL.ECSComponents;
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

        public static string AsString(this PBBillboard billboard) =>
            $"Billboard: {{x: {billboard.UseX()}; y: {billboard.UseY()}; z: {billboard.UseZ()}}}";
    }
}

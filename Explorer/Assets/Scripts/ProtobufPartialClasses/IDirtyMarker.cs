using System.Runtime.CompilerServices;

namespace DCL.ECSComponents
{
    public interface IDirtyMarker
    {
        bool IsDirty { get; set; }
    }

    public partial class PBAvatarShape : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBAvatarAttach : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBAudioSource : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBAudioStream : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBVisibilityComponent : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBGltfContainer : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBMeshCollider : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBMeshRenderer : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBTextShape : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBMaterial : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBPointerEvents : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBBillboard : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBPointerEventsResult : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBGltfContainerLoadingState : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBRaycast : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBUiTransform : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBUiText : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBUiBackground : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBUiInput : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBUiDropdown : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBVideoPlayer : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBTween : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBTweenState : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBAnimator : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBCameraModeArea : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBAvatarModifierArea : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBPlayerIdentityData : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBAvatarBase : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBAvatarEquippedData : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public partial class PBAvatarEmoteCommand : IDirtyMarker
    {
        public bool IsDirty { get; set; }
    }

    public static class DirtyMarkerExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNotDirty(this IDirtyMarker dirtyMarker) =>
            dirtyMarker.IsDirty == false;
    }
}

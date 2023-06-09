namespace DCL.ECSComponents
{
    public interface IDirtyMarker
    {
        bool IsDirty { get; set; }
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
}

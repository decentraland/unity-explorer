using DCL.ECSComponents;

namespace DCL.SDKComponents.NftShape.Renderer
{
    public interface INftShapeRenderer
    {
        void Apply(PBNftShape nftShape);

        void Hide();

        void Show();
    }
}

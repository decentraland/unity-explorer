using DCL.ECSComponents;

namespace DCL.SDKComponents.TextShape.Renderer
{
    public interface ITextShapeRenderer
    {
        void Apply(PBTextShape textShape);

        void Hide();

        void Show();
    }
}

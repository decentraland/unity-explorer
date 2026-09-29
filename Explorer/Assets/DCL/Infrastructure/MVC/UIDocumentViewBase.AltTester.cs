#if ALTTESTER
namespace MVC
{
    public abstract partial class UIDocumentViewBase
    {
        partial void ReportViewState(string state) =>
            AltTesterViewProbe.Report(GetType().Name, state);
    }
}
#endif

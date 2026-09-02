#if ALTTESTER
namespace MVC
{
    public abstract partial class ViewBase
    {
        partial void ReportViewState(string state) =>
            AltTesterViewProbe.Report(GetType().Name, state);
    }
}
#endif

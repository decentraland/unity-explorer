using CodeLess.Attributes;

namespace DCL.WebRequests.Dumper
{
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION | SingletonGenerationBehavior.GENERATE_STATIC_ACCESSORS)]
    public partial class WebRequestsDebugControl
    {
        internal bool disableCache { get; set; }
    }
}

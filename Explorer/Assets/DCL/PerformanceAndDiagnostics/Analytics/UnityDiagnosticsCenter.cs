using CodeLess.Attributes;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class UnityDiagnosticsCenter
    {
        /// <summary>Outcome of the startup hardware check; null until it runs.</summary>
        public bool? MeetsMinimumRequirements { get; private set; }

        public void SetMeetsMinimumRequirements(bool meets)
        {
            MeetsMinimumRequirements = meets;
        }
    }
}

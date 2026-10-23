using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace BevyMigration.AllowlistAnalyzer
{
    /// <summary>
    ///     Reports every use of a UnityEngine, Unity.* or TMPro member that ledger/allowlist.toml does not list.
    ///     Skeleton at step 0: the descriptor is declared, the member walk lands with the allowlist in step 1.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AllowlistAnalyzer : DiagnosticAnalyzer
    {
        public const string DIAGNOSTIC_ID = "BM0001";

        private static readonly DiagnosticDescriptor MEMBER_NOT_IN_ALLOWLIST = new (
            DIAGNOSTIC_ID,
            "Unity member outside the allowlist",
            "'{0}' is not listed in BevyMigration/ledger/allowlist.toml",
            "BevyMigration",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MEMBER_NOT_IN_ALLOWLIST);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        }
    }
}

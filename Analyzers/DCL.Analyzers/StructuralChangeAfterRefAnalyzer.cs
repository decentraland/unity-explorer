using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DCL.Analyzers
{
    /// <summary>
    ///     DCLA001: a ref local obtained from Arch's World.Get/TryGetRef is used after a
    ///     structural change (World.Add/Remove/Create/Destroy) in the same method body.
    ///     Structural changes relocate entity data in memory, so the outstanding ref points
    ///     at stale memory - reads are garbage, writes are silently lost
    ///     (CLAUDE.md § Safe Component Mutation).
    ///     Ordering is judged by linear source position within the body, refined by two
    ///     reachability carve-outs calibrated on real compiles: a use inside the structural
    ///     call's own argument list is pre-call evaluation, and a (call, use) pair in
    ///     mutually exclusive branches (if/else, switch sections, ternary arms) - the
    ///     TryGetRef-then-branch idiom - can never both execute. Residual false positives
    ///     (e.g. exclusive paths via early return) are suppressible with #pragma.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StructuralChangeAfterRefAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DCLA001";

        private const string ARCH_WORLD_METADATA_NAME = "Arch.Core.World";

        private static readonly ImmutableHashSet<string> REF_SOURCES =
            ImmutableHashSet.Create("Get", "TryGetRef");

        private static readonly ImmutableHashSet<string> STRUCTURAL_METHODS =
            ImmutableHashSet.Create("Add", "Remove", "Create", "Destroy", "AddOrGet", "AddRange", "RemoveRange");

        private static readonly DiagnosticDescriptor RULE = new (
            DiagnosticId,
            "Ref component used after a structural change",
            "ref local '{0}' is used after '{1}' - structural changes relocate entity memory and invalidate outstanding refs; complete all ref reads/writes first, or defer the structural change",
            "Correctness",
            // Error by DEFAULT: Unity's csc ignores .editorconfig dotnet_diagnostic severities
            // (verified: a probe violation compiled as a warning), so a corruption-class rule
            // only fails the Unity build if the descriptor itself says Error. The .editorconfig
            // pins still govern IDEs and dotnet builds (including the Tests downgrade).
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Structural changes (World.Add/Remove/Create/Destroy) move entity data between archetype chunks. " +
                         "Any ref obtained from World.Get/TryGetRef before the change points at the old location: " +
                         "writes are silently lost and reads observe stale data. See CLAUDE.md § Safe Component Mutation.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RULE);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? worldType = context.Compilation.GetTypeByMetadataName(ARCH_WORLD_METADATA_NAME);
            if (worldType == null) return;

            context.RegisterCodeBlockAction(blockContext => AnalyzeBlock(blockContext, worldType));
        }

        private static void AnalyzeBlock(CodeBlockAnalysisContext context, INamedTypeSymbol worldType)
        {
            if (VendoredCode.IsVendored(context.CodeBlock.SyntaxTree)) return;

            SyntaxNode block = context.CodeBlock;
            SemanticModel model = context.SemanticModel;

            // ref locals from World.Get/TryGetRef: symbol -> acquisition positions
            // (declaration plus every 'x = ref World.Get(...)' re-fetch, which restores
            // validity after a structural change - the sanctioned re-acquire idiom)
            var refLocals = new Dictionary<ILocalSymbol, List<int>>(SymbolEqualityComparer.Default);

            // structural World invocations, in source order
            var structuralCalls = new List<(InvocationExpressionSyntax node, string name)>();

            foreach (SyntaxNode node in block.DescendantNodes())
            {
                switch (node)
                {
                    case VariableDeclaratorSyntax { Initializer.Value: RefExpressionSyntax { Expression: InvocationExpressionSyntax refInvocation } } declarator:
                        if (IsWorldInvocation(refInvocation, model, worldType, REF_SOURCES, context.CancellationToken)
                            && model.GetDeclaredSymbol(declarator, context.CancellationToken) is ILocalSymbol { IsRef: true } local)
                            (refLocals.TryGetValue(local, out List<int>? dp) ? dp : refLocals[local] = new List<int>()).Add(declarator.SpanStart);
                        break;

                    case AssignmentExpressionSyntax { Right: RefExpressionSyntax { Expression: InvocationExpressionSyntax refetchInvocation }, Left: IdentifierNameSyntax lhs }:
                        if (IsWorldInvocation(refetchInvocation, model, worldType, REF_SOURCES, context.CancellationToken)
                            && model.GetSymbolInfo(lhs, context.CancellationToken).Symbol is ILocalSymbol { IsRef: true } refetched)
                            (refLocals.TryGetValue(refetched, out List<int>? rp) ? rp : refLocals[refetched] = new List<int>()).Add(lhs.SpanStart);
                        break;

                    case InvocationExpressionSyntax invocation:
                        if (IsWorldInvocation(invocation, model, worldType, STRUCTURAL_METHODS, context.CancellationToken))
                            structuralCalls.Add((invocation, GetMethodName(invocation, model, context.CancellationToken)));
                        break;
                }
            }

            if (refLocals.Count == 0 || structuralCalls.Count == 0) return;

            foreach (IdentifierNameSyntax identifier in block.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (model.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not ILocalSymbol referenced
                    || !refLocals.TryGetValue(referenced, out List<int>? acquisitions))
                    continue;

                // the acquisition governing this use is the nearest one before it; the LHS
                // of a re-fetch is itself an acquisition, not a use
                int declaredAt = int.MinValue;
                foreach (int position in acquisitions)
                {
                    if (position <= identifier.SpanStart && position > declaredAt)
                        declaredAt = position;
                }

                if (declaredAt == int.MinValue || identifier.SpanStart <= declaredAt) continue;

                // the first structural call between the declaration and this use invalidates
                // the ref - unless the "use" is inside the call's own argument list (arguments
                // evaluate before the call runs), or the two sit in mutually exclusive branches
                // (if/else, switch sections, ternary arms) and can never both execute.
                foreach ((InvocationExpressionSyntax call, string name) in structuralCalls)
                {
                    if (call.SpanStart <= declaredAt || call.SpanStart >= identifier.SpanStart) continue;
                    if (call.Span.Contains(identifier.Span)) continue;
                    if (InExclusiveBranches(call, identifier)) continue;

                    context.ReportDiagnostic(Diagnostic.Create(
                        RULE, identifier.GetLocation(), referenced.Name, $"World.{name}"));
                    break;
                }
            }
        }

        /// <summary>
        ///     True when the two nodes sit in mutually exclusive branches of a common ancestor
        ///     (then vs else of an if, different switch sections, opposite ternary arms) -
        ///     execution can reach one or the other, never both.
        /// </summary>
        private static bool InExclusiveBranches(SyntaxNode a, SyntaxNode b)
        {
            for (SyntaxNode? ancestor = a.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                switch (ancestor)
                {
                    case IfStatementSyntax { Else: { } elseClause } ifStatement:
                        bool aInThen = ifStatement.Statement.Span.Contains(a.Span);
                        bool bInThen = ifStatement.Statement.Span.Contains(b.Span);
                        bool aInElse = elseClause.Span.Contains(a.Span);
                        bool bInElse = elseClause.Span.Contains(b.Span);
                        if ((aInThen && bInElse) || (aInElse && bInThen)) return true;
                        break;

                    case SwitchStatementSyntax switchStatement when switchStatement.Span.Contains(b.Span):
                        SwitchSectionSyntax? aSection = a.FirstAncestorOrSelf<SwitchSectionSyntax>();
                        SwitchSectionSyntax? bSection = b.FirstAncestorOrSelf<SwitchSectionSyntax>();
                        if (aSection != null && bSection != null && aSection != bSection) return true;
                        break;

                    case ConditionalExpressionSyntax conditional:
                        bool aTrue = conditional.WhenTrue.Span.Contains(a.Span);
                        bool bTrue = conditional.WhenTrue.Span.Contains(b.Span);
                        bool aFalse = conditional.WhenFalse.Span.Contains(a.Span);
                        bool bFalse = conditional.WhenFalse.Span.Contains(b.Span);
                        if ((aTrue && bFalse) || (aFalse && bTrue)) return true;
                        break;
                }
            }

            return false;
        }

        private static bool IsWorldInvocation(
            InvocationExpressionSyntax invocation,
            SemanticModel model,
            INamedTypeSymbol worldType,
            ImmutableHashSet<string> methodNames,
            System.Threading.CancellationToken ct)
        {
            if (model.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method) return false;

            return methodNames.Contains(method.Name)
                   && SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, worldType);
        }

        private static string GetMethodName(InvocationExpressionSyntax invocation, SemanticModel model, System.Threading.CancellationToken ct) =>
            model.GetSymbolInfo(invocation, ct).Symbol?.Name ?? "?";
    }
}

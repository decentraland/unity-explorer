using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace DCL.Analyzers
{
    /// <summary>
    ///     DCLA003: a heap allocation inside a system's per-frame code - the Update() override
    ///     or a [Query]-attributed method (the generated update path dispatches to those, and
    ///     they run per entity, hotter than Update itself).
    ///     Per-frame code runs every frame across multiple world executions, so reference-type
    ///     construction, capturing lambdas, string interpolation/concatenation, and LINQ
    ///     calls there accumulate into GC pressure (CLAUDE.md § Performance Constraints).
    ///     The check is body-only: allocations in callees are not chased. Allocations under a
    ///     throw statement/expression are exempt - throw paths are cold, not per-frame pressure.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AllocationInSystemUpdateAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DCLA003";

        private const string BASE_SYSTEM_METADATA_NAME = "ECS.Abstract.BaseUnityLoopSystem";
        private const string BASE_SYSTEM_SIMPLE_NAME = "BaseUnityLoopSystem";
        private const string UPDATE_METHOD_NAME = "Update";

        // Arch.System.QueryAttribute, matched by name so test stubs and vendored copies work
        private const string QUERY_ATTRIBUTE_NAME = "QueryAttribute";
        private const string QUERY_ATTRIBUTE_SHORT_NAME = "Query";

        // Utility.HotPathAttribute: opt-in allocation checking outside ECS systems
        // (per-frame or per-network-call code - review-enforced hot paths)
        private const string HOT_PATH_ATTRIBUTE_NAME = "HotPathAttribute";
        private const string HOT_PATH_ATTRIBUTE_SHORT_NAME = "HotPath";

        private static readonly ImmutableHashSet<string> LINQ_TYPE_NAMES =
            ImmutableHashSet.Create("Enumerable", "Queryable");

        private static readonly DiagnosticDescriptor RULE = new (
            DiagnosticId,
            "Allocation in per-frame system code",
            "per-frame system code must be allocation-free: {0}",
            "Performance",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "System Update() overrides and the [Query] methods the generated update path dispatches to run every frame " +
                         "across multiple world executions, so per-frame heap allocations accumulate into GC pressure. " +
                         "Avoid reference-type construction, capturing lambdas, string interpolation/concatenation, and LINQ in the update path. " +
                         "See CLAUDE.md § Performance Constraints.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RULE);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? baseSystemType = context.Compilation.GetTypeByMetadataName(BASE_SYSTEM_METADATA_NAME);

            // always register: [HotPath] methods are checked in ANY assembly, including
            // ones that never reference the ECS base system (URL handlers, MCP runtime)
            context.RegisterSyntaxNodeAction(c => AnalyzeMethod(c, baseSystemType), SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol? baseSystemType)
        {
            var method = (MethodDeclarationSyntax)context.Node;

            bool updateShaped = method.Identifier.ValueText == UPDATE_METHOD_NAME
                                && method.Modifiers.Any(SyntaxKind.OverrideKeyword);

            if (!updateShaped && method.AttributeLists.Count == 0) return;

            SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
            if (body == null) return;

            IMethodSymbol? symbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
            if (symbol == null) return;

            // [HotPath] opts a method in regardless of its containing type; otherwise the
            // method must be a system's Update override or [Query] body
            if (!HasAttribute(symbol, HOT_PATH_ATTRIBUTE_NAME, HOT_PATH_ATTRIBUTE_SHORT_NAME))
            {
                if (!InheritsFromBaseSystem(symbol.ContainingType, baseSystemType))
                    return;

                if (!(updateShaped && symbol.IsOverride) && !HasQueryAttribute(symbol))
                    return;
            }

            // allocations under a throw are cold-path, not per-frame pressure: skip them
            foreach (SyntaxNode node in body.DescendantNodes(static n => n is not ThrowStatementSyntax and not ThrowExpressionSyntax))
                AnalyzeNode(node, context);
        }

        private static bool HasQueryAttribute(IMethodSymbol symbol) =>
            HasAttribute(symbol, QUERY_ATTRIBUTE_NAME, QUERY_ATTRIBUTE_SHORT_NAME);

        private static bool HasAttribute(IMethodSymbol symbol, string name, string shortName)
        {
            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.Name == name || attribute.AttributeClass?.Name == shortName)
                    return true;
            }

            return false;
        }

        private static void AnalyzeNode(SyntaxNode node, SyntaxNodeAnalysisContext context)
        {
            SemanticModel model = context.SemanticModel;
            CancellationToken ct = context.CancellationToken;

            switch (node)
            {
                case BaseObjectCreationExpressionSyntax creation:
                    // exception construction is error-path work regardless of position (thrown,
                    // wrapped into a Result, logged) - never per-frame pressure
                    if (model.GetTypeInfo(creation, ct).Type is { IsReferenceType: true } createdType
                        && !DerivesFromException(createdType, model.Compilation))
                        Report(context, node, $"'new {createdType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}' constructs a reference type");
                    break;

                case ArrayCreationExpressionSyntax:
                case ImplicitArrayCreationExpressionSyntax:
                    Report(context, node, "array creation allocates");
                    break;

                case AnonymousObjectCreationExpressionSyntax:
                    Report(context, node, "anonymous object creation allocates");
                    break;

                case AnonymousFunctionExpressionSyntax lambda:
                    DataFlowAnalysis? flow = model.AnalyzeDataFlow(lambda);

                    if (flow is { Succeeded: true } && (!flow.Captured.IsEmpty || !flow.CapturedInside.IsEmpty))
                    {
                        ISymbol? captured = flow.Captured.FirstOrDefault() ?? flow.CapturedInside.FirstOrDefault();

                        Report(context, node, captured != null
                            ? $"lambda captures '{captured.Name}' and allocates a closure"
                            : "capturing lambda allocates a closure");
                    }

                    break;

                case InterpolatedStringExpressionSyntax interpolation:
                    if (model.GetOperation(interpolation, ct)?.ConstantValue.HasValue != true)
                        Report(context, node, "string interpolation allocates");
                    break;

                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                    if (!IsStringConcat(binary, model, ct)) break;

                    // compiler-folded constant concat (literals, consts, nameof) never allocates at runtime
                    if (model.GetOperation(binary, ct)?.ConstantValue.HasValue == true) break;

                    // a chain like a + b + c is a single runtime String.Concat: report once, at the top
                    if (WalkUpParentheses(binary.Parent) is BinaryExpressionSyntax parentConcat && IsStringConcat(parentConcat, model, ct)) break;

                    Report(context, node, "string concatenation allocates");
                    break;

                // 'label += name' is String.Concat too, but as an AddAssignmentExpression it
                // never enters the AddExpression case above
                case AssignmentExpressionSyntax assignment when assignment.IsKind(SyntaxKind.AddAssignmentExpression):
                    if (model.GetTypeInfo(assignment.Left, ct).Type?.SpecialType == SpecialType.System_String
                        || model.GetTypeInfo(assignment.Right, ct).Type?.SpecialType == SpecialType.System_String)
                        Report(context, node, "string concatenation allocates");

                    break;

                case InvocationExpressionSyntax invocation:
                    if (model.GetSymbolInfo(invocation, ct).Symbol is IMethodSymbol { ContainingType: { } linqCandidate } linqMethod
                        && IsLinqType(linqCandidate))
                        Report(context, node, $"LINQ call '{linqCandidate.Name}.{linqMethod.Name}' allocates");
                    break;
            }
        }

        private static bool DerivesFromException(ITypeSymbol type, Compilation compilation)
        {
            INamedTypeSymbol? exceptionType = compilation.GetTypeByMetadataName("System.Exception");
            if (exceptionType == null) return false;

            for (ITypeSymbol? current = type; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, exceptionType))
                    return true;
            }

            return false;
        }

        private static bool InheritsFromBaseSystem(INamedTypeSymbol type, INamedTypeSymbol? baseSystemType)
        {
            // the simple-name fallback only applies when the metadata anchor did not resolve
            // (stubs / asmdef splits relocating the base class); with the real symbol present,
            // an unrelated type merely sharing the name must not activate the rule
            for (INamedTypeSymbol? current = type.BaseType; current != null; current = current.BaseType)
            {
                if (baseSystemType != null
                        ? SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseSystemType)
                        : current.Name == BASE_SYSTEM_SIMPLE_NAME)
                    return true;
            }

            return false;
        }

        private static bool IsStringConcat(BinaryExpressionSyntax binary, SemanticModel model, CancellationToken ct) =>
            binary.IsKind(SyntaxKind.AddExpression)
            && (model.GetTypeInfo(binary.Left, ct).Type?.SpecialType == SpecialType.System_String
                || model.GetTypeInfo(binary.Right, ct).Type?.SpecialType == SpecialType.System_String);

        private static bool IsLinqType(INamedTypeSymbol type) =>
            LINQ_TYPE_NAMES.Contains(type.Name)
            && type.ContainingNamespace is { Name: "Linq", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };

        private static SyntaxNode? WalkUpParentheses(SyntaxNode? node)
        {
            while (node is ParenthesizedExpressionSyntax parenthesized)
                node = parenthesized.Parent;

            return node;
        }

        private static void Report(SyntaxNodeAnalysisContext context, SyntaxNode node, string kind) =>
            context.ReportDiagnostic(Diagnostic.Create(RULE, node.GetLocation(), kind));
    }
}

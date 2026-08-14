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
    ///     DCLA002: a detached UniTask flow - an async UniTaskVoid method/local function/lambda,
    ///     or a same-file method detached via a bare UniTask .Forget() - has no exception handling
    ///     of its own. Detached flows run outside any awaiter, so an unhandled exception is
    ///     swallowed or crashes the player instead of being reported
    ///     (CLAUDE.md § Async Flow Guidelines; async-programming skill).
    ///     A flow counts as guarded when its own body contains a try with catch (System.Exception)
    ///     or a general catch, or an invocation of SuppressToResultAsync / SuppressToResult;
    ///     guards inside nested lambdas/local functions do not count (they observe only their own
    ///     flow), and neither does SuppressCancellationThrow (it only swallows cancellation, so
    ///     other exceptions still escape unobserved). Forget(exceptionHandler) is guarded at the
    ///     callsite, and an async UniTaskVoid target of .Forget() is reported once, at its
    ///     declaration - UniTaskVoid.Forget() is an intent marker, not a second detachment point.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DetachedUniTaskFlowAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DCLA002";

        private const string UNITASK_VOID_METADATA_NAME = "Cysharp.Threading.Tasks.UniTaskVoid";
        private const string EXCEPTION_METADATA_NAME = "System.Exception";
        private const string FORGET_METHOD_NAME = "Forget";

        // SuppressCancellationThrow is deliberately absent: it only swallows
        // OperationCanceledException, so it does not guard the flow
        private static readonly ImmutableHashSet<string> SUPPRESS_METHODS =
            ImmutableHashSet.Create("SuppressToResultAsync", "SuppressToResult");

        private static readonly DiagnosticDescriptor RULE = new (
            DiagnosticId,
            "Detached UniTask flow swallows exceptions",
            "detached flow '{0}' has no exception handling - wrap the body in try/catch (Exception) (ignore OperationCanceledException, report the rest via ReportHub.LogException) or chain SuppressToResultAsync",
            "Correctness",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "async UniTaskVoid flows and .Forget()-detached flows run outside any awaiter: " +
                         "nothing observes the returned task, so an unhandled exception is silently swallowed. " +
                         "Detached flows must handle their own exceptions - try/catch (Exception) that ignores " +
                         "OperationCanceledException and reports the rest via ReportHub.LogException, or a " +
                         "SuppressToResultAsync chain. See CLAUDE.md § Async Flow Guidelines and the async-programming skill.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RULE);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? uniTaskVoidType = context.Compilation.GetTypeByMetadataName(UNITASK_VOID_METADATA_NAME);
            if (uniTaskVoidType == null) return;

            INamedTypeSymbol? exceptionType = context.Compilation.GetTypeByMetadataName(EXCEPTION_METADATA_NAME);

            context.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeDeclaredFlow(nodeContext, uniTaskVoidType, exceptionType),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.LocalFunctionStatement);

            context.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeLambda(nodeContext, uniTaskVoidType, exceptionType),
                SyntaxKind.ParenthesizedLambdaExpression,
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.AnonymousMethodExpression);

            context.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeForget(nodeContext, uniTaskVoidType, exceptionType),
                SyntaxKind.InvocationExpression);
        }

        /// <summary>Rule (a) for named flows: async UniTaskVoid method or local function without its own guard.</summary>
        private static void AnalyzeDeclaredFlow(SyntaxNodeAnalysisContext context, INamedTypeSymbol uniTaskVoidType, INamedTypeSymbol? exceptionType)
        {
            if (VendoredCode.IsVendored(context.Node.SyntaxTree)) return;

            (SyntaxTokenList modifiers, SyntaxToken identifier, SyntaxNode? body) = context.Node switch
            {
                MethodDeclarationSyntax method => (method.Modifiers, method.Identifier, (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression),
                LocalFunctionStatementSyntax localFunction => (localFunction.Modifiers, localFunction.Identifier, (SyntaxNode?)localFunction.Body ?? localFunction.ExpressionBody?.Expression),
                _ => default,
            };

            if (body == null || !modifiers.Any(SyntaxKind.AsyncKeyword)) return;

            if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken) is not IMethodSymbol method2
                || !SymbolEqualityComparer.Default.Equals(method2.ReturnType, uniTaskVoidType))
                return;

            if (!IsGuarded(body, context.SemanticModel, exceptionType, context.CancellationToken))
                context.ReportDiagnostic(Diagnostic.Create(RULE, identifier.GetLocation(), identifier.ValueText));
        }

        /// <summary>Rule (a) for anonymous flows: async lambda or anonymous method returning UniTaskVoid without its own guard.</summary>
        private static void AnalyzeLambda(SyntaxNodeAnalysisContext context, INamedTypeSymbol uniTaskVoidType, INamedTypeSymbol? exceptionType)
        {
            if (VendoredCode.IsVendored(context.Node.SyntaxTree)) return;

            var lambda = (AnonymousFunctionExpressionSyntax)context.Node;

            if (!lambda.Modifiers.Any(SyntaxKind.AsyncKeyword) || lambda.Body == null) return;

            if (context.SemanticModel.GetSymbolInfo(lambda, context.CancellationToken).Symbol is not IMethodSymbol method
                || !SymbolEqualityComparer.Default.Equals(method.ReturnType, uniTaskVoidType))
                return;

            if (!IsGuarded(lambda.Body, context.SemanticModel, exceptionType, context.CancellationToken))
            {
                SyntaxToken asyncKeyword = lambda.Modifiers.First(m => m.IsKind(SyntaxKind.AsyncKeyword));
                context.ReportDiagnostic(Diagnostic.Create(RULE, asyncKeyword.GetLocation(), "anonymous function"));
            }
        }

        /// <summary>
        ///     Rule (b): '&lt;invocation&gt;.Forget()' where the invoked method is declared in the same
        ///     source file and its body has no guard. Cross-file targets are out of scope.
        /// </summary>
        private static void AnalyzeForget(SyntaxNodeAnalysisContext context, INamedTypeSymbol uniTaskVoidType, INamedTypeSymbol? exceptionType)
        {
            if (VendoredCode.IsVendored(context.Node.SyntaxTree)) return;

            var invocation = (InvocationExpressionSyntax)context.Node;

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name.Identifier.ValueText != FORGET_METHOD_NAME
                || memberAccess.Expression is not InvocationExpressionSyntax detachedCall)
                return;

            // Forget(exceptionHandler) observes exceptions at the callsite - the handler IS the guard
            if (invocation.ArgumentList.Arguments.Count > 0)
                return;

            // only UniTask's own Forget marks a detached flow; an unrelated domain method that
            // happens to be named Forget says nothing about async exception handling
            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol forgetMethod
                || !IsDeclaredInCysharp(forgetMethod))
                return;

            if (context.SemanticModel.GetSymbolInfo(detachedCall, context.CancellationToken).Symbol is not IMethodSymbol target)
                return;

            // an async UniTaskVoid target is already reported at its declaration by rule (a);
            // UniTaskVoid.Forget() is a no-op intent marker, not a second detachment point
            if (SymbolEqualityComparer.Default.Equals(target.ReturnType, uniTaskVoidType))
                return;

            // reduced extension methods declare their syntax on the unreduced form, and extended
            // partial methods carry the body on the implementation part
            IMethodSymbol definition = (target.ReducedFrom ?? target).OriginalDefinition;
            definition = definition.PartialImplementationPart ?? definition;

            foreach (SyntaxReference declaration in definition.DeclaringSyntaxReferences)
            {
                if (declaration.SyntaxTree != invocation.SyntaxTree) continue;

                SyntaxNode? body = declaration.GetSyntax(context.CancellationToken) switch
                {
                    MethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression,
                    LocalFunctionStatementSyntax localFunction => (SyntaxNode?)localFunction.Body ?? localFunction.ExpressionBody?.Expression,
                    _ => null,
                };

                if (body != null && !IsGuarded(body, context.SemanticModel, exceptionType, context.CancellationToken))
                    context.ReportDiagnostic(Diagnostic.Create(RULE, memberAccess.Name.GetLocation(), target.Name));

                return;
            }
        }

        /// <summary>
        ///     A body is guarded when it contains a catch of System.Exception (or a general catch),
        ///     or an invocation of one of the suppress methods (matched by name, so stubs work).
        ///     Nested lambdas/local functions are separate flows, so their interiors are skipped:
        ///     a guard that only wraps a nested function does not observe the outer flow's awaits.
        /// </summary>
        private static bool IsGuarded(SyntaxNode body, SemanticModel model, INamedTypeSymbol? exceptionType, CancellationToken ct)
        {
            foreach (SyntaxNode node in body.DescendantNodesAndSelf(static n => n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax))
            {
                switch (node)
                {
                    case CatchClauseSyntax { Declaration: null }:
                        return true;

                    case CatchClauseSyntax { Declaration.Type: { } caughtTypeSyntax }:
                        if (exceptionType != null
                            && SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(caughtTypeSyntax, ct).Type, exceptionType))
                            return true;

                        break;

                    case InvocationExpressionSyntax invocation:
                        if (SUPPRESS_METHODS.Contains(GetInvokedName(invocation)))
                            return true;

                        break;
                }
            }

            return false;
        }

        private static bool IsDeclaredInCysharp(IMethodSymbol method) =>
            method.ContainingNamespace is
            {
                Name: "Tasks",
                ContainingNamespace: { Name: "Threading", ContainingNamespace: { Name: "Cysharp", ContainingNamespace.IsGlobalNamespace: true } },
            };

        private static string GetInvokedName(InvocationExpressionSyntax invocation) =>
            invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => string.Empty,
            };
    }
}

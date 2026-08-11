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
    ///     DCLA004: a local rented from an object pool (ListPool/HashSetPool/DictionaryPool/
    ///     GenericPool/ObjectPool or any IObjectPool implementation) provably leaks: within its
    ///     declaring scope it is never passed to Release/Return, never returned, never stored in a
    ///     field/property, never passed to another call, never captured by a nested function,
    ///     and is not the resource of a using statement/declaration
    ///     (code-standards skill § Memory; ecs-system-and-component-design skill § cleanup).
    ///     Deliberately conservative: any escape of the rented value - including a plain
    ///     local-to-local copy or an is-pattern alias - silences the rule, so only provable leaks
    ///     are reported. Member invocations on rentals from arbitrary-object pools also silence it:
    ///     the pooled object's own method can transfer ownership (the self-release idiom, e.g.
    ///     OneShotAudioSource.Play scheduling pool.Release(this)). BCL collections rented from the
    ///     CollectionPool family cannot self-release, so member calls on them stay provably local.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PooledRentalLeakAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DCLA004";

        private const string IOBJECT_POOL_METADATA_NAME = "UnityEngine.Pool.IObjectPool`1";

        // pools renting BCL collections: the rented List/HashSet/Dictionary cannot release
        // itself back to the pool, so member invocations on such rentals stay provably local.
        // CollectionPool`2 is required: Unity's static ListPool/HashSetPool/DictionaryPool
        // inherit Get/Release from it, so that's where the method symbol actually lives.
        private static readonly ImmutableHashSet<string> COLLECTION_POOL_METADATA_NAMES = ImmutableHashSet.Create(
            "UnityEngine.Pool.ListPool`1",
            "UnityEngine.Pool.HashSetPool`1",
            "UnityEngine.Pool.DictionaryPool`2",
            "UnityEngine.Pool.CollectionPool`2");

        // matched against the containing type of the resolved Get() plus its base types and
        // interfaces, by metadata name only (test stubs and Unity assemblies both match)
        private static readonly ImmutableHashSet<string> POOL_TYPE_METADATA_NAMES = COLLECTION_POOL_METADATA_NAMES.Union(new[]
        {
            "UnityEngine.Pool.ObjectPool`1",
            "UnityEngine.Pool.GenericPool`1",
            "UnityEngine.Pool.UnsafeGenericPool`1",
            "UnityEngine.Pool.LinkedPool`1",
            IOBJECT_POOL_METADATA_NAME,
            "DCL.Optimization.Pools.IExtendedObjectPool`1",
        });

        private static readonly DiagnosticDescriptor RULE = new (
            DiagnosticId,
            "Pooled rental provably leaks",
            "pooled object '{0}' rented from '{1}' is never released and never leaves this method - it provably leaks; release it via '{1}.Release' (ideally in a finally block) or rent through PoolExtensions.AutoScope",
            "Correctness",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A local rented with Get() from an object pool (UnityEngine.Pool.ListPool/HashSetPool/" +
                         "DictionaryPool/GenericPool/ObjectPool or any IObjectPool implementation such as " +
                         "DCL.Optimization.Pools.IExtendedObjectPool) that is never passed to Release/Return and never " +
                         "escapes the method (returned, stored in a field, passed as an argument, captured, or scoped by " +
                         "a using) permanently removes the instance from the pool. See the code-standards skill § Memory " +
                         "and the ecs-system-and-component-design skill § cleanup (CLAUDE.md § Component Clean-up Patterns).");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RULE);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            // anchor on any known pool surface: no pool types, nothing to rent from.
            // GetTypesByMetadataName (plural) stays resolvable when a name is ambiguously
            // defined in several referenced assemblies (stub/shim packages), where the
            // singular GetTypeByMetadataName returns null and would disable the rule.
            foreach (string poolTypeName in POOL_TYPE_METADATA_NAMES)
            {
                if (!context.Compilation.GetTypesByMetadataName(poolTypeName).IsEmpty)
                {
                    context.RegisterCodeBlockAction(AnalyzeBlock);
                    return;
                }
            }
        }

        private static void AnalyzeBlock(CodeBlockAnalysisContext context)
        {
            SyntaxNode block = context.CodeBlock;
            SemanticModel model = context.SemanticModel;

            // locals assigned from a parameterless pool Get(); Get(out T) returns a
            // PooledObject/scope that releases on Dispose, so it is never a rental here.
            // each rental remembers its pool family and the nested function (lambda/local
            // function) it was declared in, so uses are judged against the declaring scope
            List<(ILocalSymbol local, InvocationExpressionSyntax get)>? rentals = null;
            Dictionary<ILocalSymbol, (bool isCollectionRental, SyntaxNode? declaringFunction)>? rentalInfo = null;

            foreach (SyntaxNode node in block.DescendantNodes())
            {
                switch (node)
                {
                    case VariableDeclaratorSyntax { Initializer: { } initializer } declarator:
                        if (Unwrap(initializer.Value) is InvocationExpressionSyntax declaratorGet
                            && IsPoolGet(declaratorGet, model, context.CancellationToken, out bool declaratorCollectionRental)
                            && !IsUsingResource(declarator)
                            && model.GetDeclaredSymbol(declarator, context.CancellationToken) is ILocalSymbol declaredLocal)
                        {
                            (rentals ??= new List<(ILocalSymbol, InvocationExpressionSyntax)>()).Add((declaredLocal, declaratorGet));
                            (rentalInfo ??= new Dictionary<ILocalSymbol, (bool, SyntaxNode?)>(SymbolEqualityComparer.Default))[declaredLocal] =
                                (declaratorCollectionRental, NearestEnclosingFunction(declarator, block));
                        }

                        break;

                    case AssignmentExpressionSyntax assignment when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                        // assignment to anything but a local (field, property, ...) is an escape by
                        // definition. an assignment USED AS AN EXPRESSION (Attach(x = pool.Get()))
                        // escapes too - the assignment's value flows into the surrounding argument/
                        // initializer - so only statement-level assignments register a rental
                        if (assignment.Parent is ExpressionStatementSyntax
                            && Unwrap(assignment.Right) is InvocationExpressionSyntax assignmentGet
                            && IsPoolGet(assignmentGet, model, context.CancellationToken, out bool assignmentCollectionRental)
                            && assignment.Left is IdentifierNameSyntax target
                            && model.GetSymbolInfo(target, context.CancellationToken).Symbol is ILocalSymbol assignedLocal)
                        {
                            (rentals ??= new List<(ILocalSymbol, InvocationExpressionSyntax)>()).Add((assignedLocal, assignmentGet));
                            (rentalInfo ??= new Dictionary<ILocalSymbol, (bool, SyntaxNode?)>(SymbolEqualityComparer.Default))[assignedLocal] =
                                (assignmentCollectionRental, NearestEnclosingFunction(assignment, block));
                        }

                        break;
                }
            }

            if (rentals == null || rentalInfo == null) return;

            var disqualified = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);

            foreach (IdentifierNameSyntax identifier in block.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (model.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not ILocalSymbol referenced
                    || !rentalInfo.TryGetValue(referenced, out (bool isCollectionRental, SyntaxNode? declaringFunction) info)
                    || IsSafeLocalUse(identifier, block, info.isCollectionRental, info.declaringFunction))
                    continue;

                disqualified.Add(referenced);
            }

            foreach ((ILocalSymbol local, InvocationExpressionSyntax get) in rentals)
            {
                if (disqualified.Contains(local)) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    RULE, get.GetLocation(), local.Name, PoolDisplayName(get)));
            }
        }

        /// <summary>Uses that keep the rented value inside its declaring scope: member/element access on it, comparisons, reassigning it, iterating it.</summary>
        private static bool IsSafeLocalUse(IdentifierNameSyntax identifier, SyntaxNode block, bool isCollectionRental, SyntaxNode? declaringFunction)
        {
            // a reference inside a nested function other than the one declaring the rental is a
            // capture: the rented value escapes its declaring scope. references in the declaring
            // function itself (including a rental declared and used within one lambda) stay local
            if (NearestEnclosingFunction(identifier, block) != declaringFunction)
                return false;

            SyntaxNode child = identifier;
            SyntaxNode? parent = identifier.Parent;

            while (parent is ParenthesizedExpressionSyntax
                   || (parent is PostfixUnaryExpressionSyntax suppression && suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression)))
            {
                child = parent;
                parent = parent.Parent;
            }

            return parent switch
            {
                // a member INVOCATION on an arbitrary pooled object can transfer ownership (the
                // self-release idiom: the object's own method schedules pool.Release(this)), so it
                // silences the rule; BCL collections rented from the CollectionPool family cannot
                // self-release, so member calls on them stay provably local. plain property/field
                // access is a local read either way
                MemberAccessExpressionSyntax memberAccess => memberAccess.Expression == child
                                                             && (isCollectionRental || !IsInvocationReceiver(memberAccess)),
                ConditionalAccessExpressionSyntax conditionalAccess => conditionalAccess.Expression == child
                                                                       && (isCollectionRental || conditionalAccess.WhenNotNull is not InvocationExpressionSyntax),
                ElementAccessExpressionSyntax elementAccess => elementAccess.Expression == child,
                AssignmentExpressionSyntax assignment => assignment.Left == child,
                CommonForEachStatementSyntax forEach => forEach.Expression == child,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression) => true,

                // a designation-free pattern ('is null', 'is { Count: > 0 }') only reads; a
                // designation binds an alias the rental can be released through, which must
                // silence the rule like any other alias copy
                IsPatternExpressionSyntax isPattern => !isPattern.Pattern.DescendantNodesAndSelf().Any(static n => n is SingleVariableDesignationSyntax),

                // everything else - argument (including Release/Return), return, initializer of
                // another variable, RHS of an assignment, using resource - silences the rule
                _ => false,
            };
        }

        private static bool IsInvocationReceiver(MemberAccessExpressionSyntax memberAccess) =>
            memberAccess.Parent is InvocationExpressionSyntax invocation && invocation.Expression == memberAccess;

        private static SyntaxNode? NearestEnclosingFunction(SyntaxNode node, SyntaxNode block)
        {
            for (SyntaxNode? current = node.Parent; current != null && current != block; current = current.Parent)
            {
                if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                    return current;
            }

            return null;
        }

        /// <summary>Strips parentheses and null-suppression (!) so 'ListPool&lt;T&gt;.Get()!' still reads as the Get() invocation.</summary>
        private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
        {
            while (true)
            {
                switch (expression)
                {
                    case ParenthesizedExpressionSyntax parenthesized:
                        expression = parenthesized.Expression;
                        break;

                    case PostfixUnaryExpressionSyntax suppression when suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                        expression = suppression.Operand;
                        break;

                    default:
                        return expression;
                }
            }
        }

        private static bool IsPoolGet(InvocationExpressionSyntax invocation, SemanticModel model, System.Threading.CancellationToken ct, out bool isCollectionRental)
        {
            isCollectionRental = false;

            if (invocation.ArgumentList.Arguments.Count != 0) return false;

            return model.GetSymbolInfo(invocation, ct).Symbol is IMethodSymbol { Name: "Get" } method
                   && IsPoolType(method.ContainingType, out isCollectionRental);
        }

        private static bool IsPoolType(INamedTypeSymbol type, out bool isCollectionRental)
        {
            for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
            {
                string metadataName = FullMetadataName(current.OriginalDefinition);

                if (POOL_TYPE_METADATA_NAMES.Contains(metadataName))
                {
                    isCollectionRental = COLLECTION_POOL_METADATA_NAMES.Contains(metadataName);
                    return true;
                }
            }

            foreach (INamedTypeSymbol implemented in type.AllInterfaces)
            {
                string metadataName = FullMetadataName(implemented.OriginalDefinition);

                if (POOL_TYPE_METADATA_NAMES.Contains(metadataName))
                {
                    isCollectionRental = COLLECTION_POOL_METADATA_NAMES.Contains(metadataName);
                    return true;
                }
            }

            isCollectionRental = false;
            return false;
        }

        private static string FullMetadataName(INamedTypeSymbol type)
        {
            string name = type.MetadataName;

            for (INamespaceSymbol? ns = type.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
                name = ns.Name + "." + name;

            return name;
        }

        private static bool IsUsingResource(VariableDeclaratorSyntax declarator) =>
            declarator.Parent is VariableDeclarationSyntax declaration
            && (declaration.Parent is UsingStatementSyntax
                || (declaration.Parent is LocalDeclarationStatementSyntax localDeclaration && localDeclaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)));

        private static string PoolDisplayName(InvocationExpressionSyntax get) =>
            get.Expression is MemberAccessExpressionSyntax memberAccess ? memberAccess.Expression.ToString() : get.Expression.ToString();
    }
}

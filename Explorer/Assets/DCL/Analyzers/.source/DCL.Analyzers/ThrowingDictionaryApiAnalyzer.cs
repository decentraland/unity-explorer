using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Immutable;

namespace DCL.Analyzers
{
    /// <summary>
    ///     Reports dictionary members that throw on a duplicate or missing key: <c>Add</c>, <c>ToDictionary</c> and indexer reads.
    ///     Indexer writes (<c>map[key] = value</c>) upsert and never throw, so they are allowed.
    ///     Covers every implementation of <c>IDictionary&lt;,&gt;</c> / <c>IReadOnlyDictionary&lt;,&gt;</c> and the Unity.Collections hash maps.
    ///     Only sources under <c>Assets/</c> are reported: Unity passes the analyzer to package assemblies too, and that code is not ours to change.
    ///     Test assemblies (<c>*.Tests</c>, <c>*.TestSuite</c>) are skipped: a missing or duplicate key there already fails the test.
    ///     They are matched by name because Unity's Editor compile references NUnit from almost every assembly.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ThrowingDictionaryApiAnalyzer : DiagnosticAnalyzer
    {
        public const string THROWING_ADD_ID = "DCL0001";
        public const string THROWING_READ_ID = "DCL0002";

        private const string CATEGORY = "Reliability";
        private const string HELP_LINK = "https://github.com/decentraland/unity-explorer/blob/dev/docs/code-style-guidelines.md#throwing-dictionary-apis";

        private static readonly ImmutableHashSet<string> NATIVE_MAP_NAMES = ImmutableHashSet.Create(
            "NativeHashMap", "NativeParallelHashMap", "UnsafeHashMap", "UnsafeParallelHashMap");

        internal static readonly DiagnosticDescriptor THROWING_ADD = new (
            THROWING_ADD_ID,
            "Dictionary insertion that throws on a duplicate key",
            "'{0}' throws when the key is already present; use TryAdd and handle the duplicate, or assign through the indexer if the latest value should win",
            CATEGORY,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HELP_LINK);

        internal static readonly DiagnosticDescriptor THROWING_READ = new (
            THROWING_READ_ID,
            "Dictionary indexer read that throws on a missing key",
            "Reading '{0}' through the indexer throws when the key is missing; use TryGetValue and handle the miss",
            CATEGORY,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HELP_LINK);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(THROWING_ADD, THROWING_READ);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationContext =>
            {
                if (IsTestAssembly(compilationContext.Compilation))
                    return;

                var symbols = new WellKnownSymbols(compilationContext.Compilation);

                compilationContext.RegisterOperationAction(c => AnalyzeInvocation(c, symbols), OperationKind.Invocation);
                compilationContext.RegisterOperationAction(c => AnalyzePropertyReference(c, symbols), OperationKind.PropertyReference);
            });
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context, WellKnownSymbols symbols)
        {
            var invocation = (IInvocationOperation)context.Operation;

            if (!IsProjectSource(invocation.Syntax.SyntaxTree.FilePath))
                return;

            IMethodSymbol method = invocation.TargetMethod;

            if (method.Name == "ToDictionary" && SymbolEqualityComparer.Default.Equals(method.ContainingType, symbols.Enumerable))
            {
                context.ReportDiagnostic(Diagnostic.Create(THROWING_ADD, invocation.Syntax.GetLocation(), "Enumerable.ToDictionary"));
                return;
            }

            if (method.Name != "Add" || method.IsExtensionMethod || method.IsStatic)
                return;

            // `new Dictionary<,> { { k, v } }`: a literal table, a duplicate fails on first use rather than in production
            if (invocation.IsImplicit && invocation.Parent is IObjectOrCollectionInitializerOperation)
                return;

            if (!symbols.IsMapType(method.ContainingType) || IsImplementingThrowingMember(context.ContainingSymbol, symbols))
                return;

            context.ReportDiagnostic(Diagnostic.Create(THROWING_ADD, invocation.Syntax.GetLocation(), $"{method.ContainingType.Name}.Add"));
        }

        private static void AnalyzePropertyReference(OperationAnalysisContext context, WellKnownSymbols symbols)
        {
            var reference = (IPropertyReferenceOperation)context.Operation;

            if (!IsProjectSource(reference.Syntax.SyntaxTree.FilePath))
                return;

            IPropertySymbol property = reference.Property;

            if (!property.IsIndexer || !symbols.IsMapType(property.ContainingType))
                return;

            // JObject's getter returns null for a missing property instead of throwing
            if (SymbolEqualityComparer.Default.Equals(property.ContainingType, symbols.JObject))
                return;

            // `map[k] = v` only calls the setter; compound assignments (`+=`, `??=`) and increments read first
            if (reference.Parent is ISimpleAssignmentOperation assignment && assignment.Target == reference)
                return;

            if (IsImplementingThrowingMember(context.ContainingSymbol, symbols))
                return;

            context.ReportDiagnostic(Diagnostic.Create(THROWING_READ, reference.Syntax.GetLocation(), $"{property.ContainingType.Name}[key]"));
        }

        private static bool IsTestAssembly(Compilation compilation)
        {
            string? name = compilation.AssemblyName;
            return name != null && (name.EndsWith(".Tests", StringComparison.Ordinal) || name.EndsWith(".TestSuite", StringComparison.Ordinal));
        }

        /// <summary>
        ///     Unity passes project-relative paths (<c>Assets/...</c>, <c>Library/PackageCache/...</c>); generated IDE projects may pass absolute ones.
        /// </summary>
        private static bool IsProjectSource(string path)
        {
            if (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Assets\\", StringComparison.Ordinal))
                return true;

            return (path.IndexOf("/Assets/", StringComparison.Ordinal) >= 0 || path.IndexOf("\\Assets\\", StringComparison.Ordinal) >= 0)
                   && path.IndexOf("PackageCache", StringComparison.Ordinal) < 0;
        }

        /// <summary>
        ///     True inside the <c>Add</c> method or indexer getter of a map type: forwarding to the inner map there implements the same throwing contract.
        /// </summary>
        private static bool IsImplementingThrowingMember(ISymbol containingSymbol, WellKnownSymbols symbols) =>
            containingSymbol is IMethodSymbol { Name: "Add" or "get_Item" } method && symbols.IsMapType(method.ContainingType);

        private sealed class WellKnownSymbols
        {
            public readonly INamedTypeSymbol? Enumerable;
            public readonly INamedTypeSymbol? JObject;

            private readonly INamedTypeSymbol? dictionaryInterface;
            private readonly INamedTypeSymbol? readOnlyDictionaryInterface;

            public WellKnownSymbols(Compilation compilation)
            {
                Enumerable = compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                JObject = compilation.GetTypeByMetadataName("Newtonsoft.Json.Linq.JObject");
                dictionaryInterface = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2");
                readOnlyDictionaryInterface = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2");
            }

            public bool IsMapType(INamedTypeSymbol? type)
            {
                if (type == null)
                    return false;

                if (IsDictionaryInterface(type) || IsNativeMap(type))
                    return true;

                foreach (INamedTypeSymbol implemented in type.AllInterfaces)
                    if (IsDictionaryInterface(implemented))
                        return true;

                return false;
            }

            private bool IsDictionaryInterface(INamedTypeSymbol type) =>
                SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, dictionaryInterface)
                || SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, readOnlyDictionaryInterface);

            /// <summary>
            ///     The map itself or one of its nested views (<c>ReadOnly</c>, <c>ParallelWriter</c>). Multi hash maps are excluded: their <c>Add</c> never throws.
            /// </summary>
            private static bool IsNativeMap(INamedTypeSymbol type)
            {
                for (INamedTypeSymbol? current = type; current != null; current = current.ContainingType)
                    if (NATIVE_MAP_NAMES.Contains(current.Name) && current.ContainingNamespace.ToDisplayString().StartsWith("Unity.Collections"))
                        return true;

                return false;
            }
        }
    }
}

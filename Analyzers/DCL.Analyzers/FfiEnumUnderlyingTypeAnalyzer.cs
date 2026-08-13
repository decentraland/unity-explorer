using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace DCL.Analyzers
{
    /// <summary>
    ///     DCLA005: an enum crossing a [DllImport] boundary (parameter, return type, or a
    ///     field of a struct parameter) is declared without an explicit underlying type.
    ///     The native side compiles against a fixed ABI layout; C#'s implicit int default
    ///     is a convention the enum author can silently change, so FFI enums must pin it
    ///     (': byte', ': int', ...) - review-enforced in PR #9088.
    ///     Only source-declared enums are checked: metadata enums cannot reveal whether
    ///     their base was written explicitly.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FfiEnumUnderlyingTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DCLA005";

        private const string DLL_IMPORT_METADATA_NAME = "System.Runtime.InteropServices.DllImportAttribute";

        private static readonly DiagnosticDescriptor RULE = new (
            DiagnosticId,
            "FFI enum without explicit underlying type",
            "enum '{0}' crosses a DllImport boundary{1} but does not declare its underlying type - pin the ABI with ': byte', ': int', ...",
            "Correctness",
            // Error by DEFAULT: Unity's csc ignores .editorconfig dotnet_diagnostic severities
            // (verified: a probe violation compiled as a warning), so a corruption-class rule
            // only fails the Unity build if the descriptor itself says Error. The .editorconfig
            // pins still govern IDEs and dotnet builds (including the Tests downgrade).
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Native code compiles against a fixed layout; an enum without an explicit base relies on " +
                         "C#'s implicit int, which nothing pins at the interop boundary. Review-enforced (PR #9088).");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RULE);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? dllImportType = context.Compilation.GetTypeByMetadataName(DLL_IMPORT_METADATA_NAME);
            if (dllImportType == null) return;

            context.RegisterSyntaxNodeAction(c => AnalyzeMethod(c, dllImportType), SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol dllImportType)
        {
            if (VendoredCode.IsVendored(context.Node.SyntaxTree)) return;

            var method = (MethodDeclarationSyntax)context.Node;

            if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not { } symbol
                || !HasDllImport(symbol, dllImportType))
                return;

            CheckType(context, symbol.ReturnType, method.ReturnType.GetLocation());

            foreach (IParameterSymbol parameter in symbol.Parameters)
            {
                Location location = parameter.DeclaringSyntaxReferences.Length > 0
                    ? parameter.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken).GetLocation()
                    : method.Identifier.GetLocation();

                CheckType(context, parameter.Type, location);
            }
        }

        private static bool HasDllImport(IMethodSymbol symbol, INamedTypeSymbol dllImportType)
        {
            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, dllImportType))
                    return true;
            }

            return false;
        }

        private static void CheckType(SyntaxNodeAnalysisContext context, ITypeSymbol type, Location location)
        {
            if (LacksExplicitUnderlyingType(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(RULE, location, type.Name, ""));
                return;
            }

            // one level into struct parameters: FFI structs marshal their fields
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsUnmanagedType: true } structType
                && structType.DeclaringSyntaxReferences.Length > 0)
            {
                foreach (ISymbol member in structType.GetMembers())
                {
                    if (member is IFieldSymbol { IsStatic: false, IsConst: false } field
                        && LacksExplicitUnderlyingType(field.Type))
                        context.ReportDiagnostic(Diagnostic.Create(
                            RULE, location, field.Type.Name, $" (field '{field.Name}' of struct '{structType.Name}')"));
                }
            }
        }

        private static bool LacksExplicitUnderlyingType(ITypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Enum) return false;

            foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is EnumDeclarationSyntax declaration)
                    return declaration.BaseList == null;
            }

            return false; // metadata enum - explicitness is unknowable, stay silent
        }
    }
}

using Microsoft.CodeAnalysis;
using System.Text;

namespace BevyMigration.Tools.ApiInventory
{
    /// <summary>
    ///     Names a Unity member the way allowlist.toml does: <c>UnityEngine.GameObject..ctor(string)</c>,
    ///     <c>UnityEngine.GameObject.GetComponent&lt;T&gt;()</c>, <c>UnityEngine.Renderer.enabled</c>,
    ///     <c>UnityEngine.Material.SetFloat(int,float)</c>.
    /// </summary>
    internal static class MemberId
    {
        private static readonly SymbolDisplayFormat TYPE_FORMAT = new (
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

        private static readonly SymbolDisplayFormat PARAMETER_TYPE_FORMAT = new (
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        /// <summary>
        ///     The namespace root the symbol belongs to (<c>UnityEngine</c>, <c>Unity</c>, <c>TMPro</c>, <c>Cinemachine</c>,
        ///     <c>UnityEditor</c>), or null when the symbol is not a Unity member. Cinemachine 2.x lives in its own root.
        /// </summary>
        public static string? UnityRoot(ISymbol symbol)
        {
            INamespaceSymbol? ns = symbol.ContainingNamespace;

            while (ns is { IsGlobalNamespace: false })
            {
                if (ns.ContainingNamespace is { IsGlobalNamespace: true })
                    return ns.Name switch
                    {
                        "UnityEngine" or "Unity" or "TMPro" or "Cinemachine" or "UnityEditor" => ns.Name,
                        _ => null,
                    };

                ns = ns.ContainingNamespace;
            }

            return null;
        }

        /// <summary>
        ///     Maps a bound symbol to the declaration the shim must provide (generic definitions, unreduced extension
        ///     methods), or null for symbols that are not members (namespaces, locals, type parameters).
        /// </summary>
        public static ISymbol? Normalize(ISymbol symbol)
        {
            switch (symbol)
            {
                case IAliasSymbol alias:
                    return Normalize(alias.Target);
                case IMethodSymbol method:
                    return (method.ReducedFrom ?? method).OriginalDefinition;
                case IPropertySymbol or IFieldSymbol or IEventSymbol:
                    return symbol.OriginalDefinition;
                case INamedTypeSymbol type:
                    return type.OriginalDefinition;
                case IArrayTypeSymbol array:
                    return Normalize(array.ElementType);
                case IPointerTypeSymbol pointer:
                    return Normalize(pointer.PointedAtType);
                default:
                    return null;
            }
        }

        public static string Kind(ISymbol symbol) =>
            symbol switch
            {
                IMethodSymbol { MethodKind: MethodKind.Constructor } => "ctor",
                IMethodSymbol => "method",
                IPropertySymbol => "property",
                IFieldSymbol => "field",
                IEventSymbol => "event",
                _ => "type",
            };

        public static string Of(ISymbol symbol)
        {
            if (symbol is INamedTypeSymbol type)
                return type.ToDisplayString(TYPE_FORMAT);

            var builder = new StringBuilder();
            builder.Append(symbol.ContainingType.ToDisplayString(TYPE_FORMAT)).Append('.');

            switch (symbol)
            {
                case IMethodSymbol method:
                    builder.Append(method.MethodKind == MethodKind.Constructor ? ".ctor" : method.Name);
                    AppendTypeParameters(builder, method);
                    AppendParameters(builder, method.Parameters);
                    break;
                case IPropertySymbol { IsIndexer: true } indexer:
                    builder.Append("this");
                    AppendParameters(builder, indexer.Parameters, '[', ']');
                    break;
                default:
                    builder.Append(symbol.Name);
                    break;
            }

            return builder.ToString();
        }

        /// <summary>
        ///     The group a member belongs to is decided on its containing type, so the type id is what the subsystem
        ///     table matches against.
        /// </summary>
        public static string ContainingTypeId(ISymbol symbol) =>
            symbol is INamedTypeSymbol type ? type.ToDisplayString(TYPE_FORMAT) : symbol.ContainingType.ToDisplayString(TYPE_FORMAT);

        private static void AppendTypeParameters(StringBuilder builder, IMethodSymbol method)
        {
            if (method.TypeParameters.Length == 0) return;

            builder.Append('<');

            for (var i = 0; i < method.TypeParameters.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(method.TypeParameters[i].Name);
            }

            builder.Append('>');
        }

        private static void AppendParameters(StringBuilder builder, System.Collections.Immutable.ImmutableArray<IParameterSymbol> parameters, char open = '(', char close = ')')
        {
            builder.Append(open);

            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0) builder.Append(',');

                switch (parameters[i].RefKind)
                {
                    case RefKind.Ref: builder.Append("ref "); break;
                    case RefKind.Out: builder.Append("out "); break;
                    case RefKind.In: builder.Append("in "); break;
                }

                builder.Append(parameters[i].Type.ToDisplayString(PARAMETER_TYPE_FORMAT));
            }

            builder.Append(close);
        }
    }
}

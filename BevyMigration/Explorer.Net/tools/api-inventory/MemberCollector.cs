using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace BevyMigration.Tools.ApiInventory
{
    /// <summary>
    ///     Walks one syntax tree and records every Unity member its bound symbols touch: names, constructors,
    ///     indexers and user-defined operators. Unresolved symbols are counted, not guessed.
    /// </summary>
    internal sealed class MemberCollector : CSharpSyntaxWalker
    {
        private readonly SemanticModel model;
        private readonly Dictionary<string, MemberUse> members;
        private readonly string file;
        private readonly Dictionary<string, int>? unresolvedNames;

        public int Unresolved { get; private set; }

        internal MemberCollector(SemanticModel model, Dictionary<string, MemberUse> members, string file, Dictionary<string, int>? unresolvedNames = null) : base(SyntaxWalkerDepth.Node)
        {
            this.model = model;
            this.members = members;
            this.file = file;
            this.unresolvedNames = unresolvedNames;
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            Record(node);
            base.VisitIdentifierName(node);
        }

        public override void VisitGenericName(GenericNameSyntax node)
        {
            Record(node);
            base.VisitGenericName(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            Record(node);
            base.VisitObjectCreationExpression(node);
        }

        public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
        {
            Record(node);
            base.VisitImplicitObjectCreationExpression(node);
        }

        public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            Record(node);
            base.VisitElementAccessExpression(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            Record(node);
            base.VisitBinaryExpression(node);
        }

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            Record(node);
            base.VisitPrefixUnaryExpression(node);
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (!node.IsKind(SyntaxKind.SimpleAssignmentExpression))
                Record(node);

            base.VisitAssignmentExpression(node);
        }

        private void Record(SyntaxNode node)
        {
            SymbolInfo info = model.GetSymbolInfo(node);
            ISymbol? symbol = info.Symbol;

            if (symbol == null)
            {
                if (info.CandidateSymbols.Length > 0)
                    symbol = info.CandidateSymbols[0];
                else if (node is IdentifierNameSyntax or GenericNameSyntax && info.CandidateReason == CandidateReason.None && model.GetTypeInfo(node).Type == null && IsExpressionPosition(node))
                {
                    Unresolved++;

                    if (unresolvedNames != null)
                    {
                        string name = node.ToString() + "\t" + file;
                        unresolvedNames[name] = unresolvedNames.TryGetValue(name, out int seen) ? seen + 1 : 1;
                    }
                }
            }

            if (symbol == null) return;

            ISymbol? normalized = MemberId.Normalize(symbol);
            if (normalized == null) return;

            string? root = MemberId.UnityRoot(normalized);
            if (root == null) return;

            string id = MemberId.Of(normalized);

            if (!members.TryGetValue(id, out MemberUse? use))
            {
                use = new MemberUse(id, MemberId.Kind(normalized), root, MemberId.ContainingTypeId(normalized));
                members[id] = use;
            }

            use.Add(file);
        }

        private static bool IsExpressionPosition(SyntaxNode node)
        {
            SyntaxNode? parent = node.Parent;

            if (parent is UsingDirectiveSyntax or NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax)
                return false;

            return parent is not QualifiedNameSyntax qualified || !ReferenceEquals(qualified.Left, node);
        }
    }
}

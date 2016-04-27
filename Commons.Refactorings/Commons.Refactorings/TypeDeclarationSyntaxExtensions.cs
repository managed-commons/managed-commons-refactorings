using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Commons.Refactorings
{
    public static class TypeDeclarationSyntaxExtensions
    {
        public const int MaximumNumberOfMembers = 12;

        public static TypeDeclarationSyntax AddPartialModifier(this TypeDeclarationSyntax typeDecl)
        {
            return typeDecl.IsPartial() ? typeDecl : typeDecl.InsertTokensAfter(typeDecl.Modifiers[0], SyntaxFactory.ParseTokens("partial "));
        }

        public static async Task<SyntaxNode> AsNewCompilationUnit(this TypeDeclarationSyntax typeDecl, Document document, CancellationToken cancellationToken)
        {
            var newRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var nodesToRemove = typeDecl.FilterSiblingsInNamespace(n => !n.Equals(typeDecl));
            var cleanedRoot = newRoot.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia);
            return Formatter.Format(cleanedRoot, document.Project.Solution.Workspace, cancellationToken: cancellationToken);
        }

        public static List<TypeDeclarationSyntax> Break(this TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var partials = new List<TypeDeclarationSyntax>();
            typeDecl = typeDecl.RemoveRegions();
            var allMembers = typeDecl.Members.ToList();
            var allMembersCount = allMembers.Count;
            if (allMembersCount <= MaximumNumberOfMembers) {
                partials.Add(typeDecl);
                return partials;
            }
            typeDecl = typeDecl.AddPartialModifier();
            allMembers = typeDecl.Members.ToList();
            allMembersCount = allMembers.Count;
            var first = typeDecl.RemoveMembers(allMembers.Skip(MaximumNumberOfMembers));
            partials.Add(first.WithoutTrailingTrivia());
            var membersMoved = MaximumNumberOfMembers;
            while (!(cancellationToken.IsCancellationRequested || membersMoved >= allMembersCount)) {
                var membersToInsert = allMembers.Skip(membersMoved).Take(MaximumNumberOfMembers).ToArray();
                var tree = SyntaxFactory.ParseSyntaxTree($"{typeDecl.Modifiers.ToString()} class {typeDecl.Identifier} {"{ private int dummy; }"}");
                var nextPartial = tree.GetRoot(cancellationToken).DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                nextPartial = nextPartial.InsertNodesAfter(nextPartial.DescendantNodes().First(), membersToInsert);
                nextPartial = nextPartial.RemoveNode(nextPartial.DescendantNodes().First(), SyntaxRemoveOptions.KeepNoTrivia);
                partials.Add(nextPartial.WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.LineFeed));
                membersMoved += MaximumNumberOfMembers;
            }
            return partials;
        }

        public static string BuildNewPartialSourceName(this TypeDeclarationSyntax typeDecl)
        {
            var methodNode = (MethodDeclarationSyntax)typeDecl.Members.FirstOrDefault(m => m is MethodDeclarationSyntax);
            var suffix = methodNode?.Identifier.Text ?? "Partial";
            return typeDecl.Identifier.Text + '-' + suffix;
        }

        public static IEnumerable<SyntaxNode> FilterSiblingsInNamespace(this TypeDeclarationSyntax typeDecl, Func<SyntaxNode, bool> filter)
        {
            return (typeDecl.Parent as NamespaceDeclarationSyntax)?.Members.Where(filter) ?? EmptyList;
        }

        public static bool HasManyPartialsInSameSource(this TypeDeclarationSyntax typeDecl)
        {
            return typeDecl.IsPartial() && CountOfPartialSiblings(typeDecl) > 1;
        }

        public static bool HasTooManyMembers(this TypeDeclarationSyntax typeDecl)
        {
            return typeDecl.Members.Count > MaximumNumberOfMembers;
        }

        private static List<SyntaxNode> EmptyList = new List<SyntaxNode>();

        private static int CountOfPartialSiblings(TypeDeclarationSyntax typeDecl)
        {
            return typeDecl.FilterSiblingsInNamespace(n => HasSameName(n, typeDecl)).Count();
        }

        private static bool HasSameName(SyntaxNode node, TypeDeclarationSyntax typeDecl)
        {
            var typeDeclarationSyntax = (node as TypeDeclarationSyntax);
            return typeDeclarationSyntax.IsPartial() && typeDeclarationSyntax?.Identifier.Text == typeDecl.Identifier.Text;
        }

        private static bool IsPartial(this TypeDeclarationSyntax typeDecl)
        {
            if (typeDecl == null)
                return false;
            return typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
        }

        private static TypeDeclarationSyntax RemoveMembers(this TypeDeclarationSyntax typeDecl, IEnumerable<MemberDeclarationSyntax> methods)
        {
            return typeDecl.RemoveNodes(methods, SyntaxRemoveOptions.KeepNoTrivia);
        }

        private static TypeDeclarationSyntax RemoveRegions(this TypeDeclarationSyntax typeDecl)
        {
            var descendants = typeDecl.DescendantNodes(descendIntoTrivia: true);
            var beginRegions = descendants.OfType<RegionDirectiveTriviaSyntax>();
            typeDecl = typeDecl.RemoveNodes(beginRegions, SyntaxRemoveOptions.KeepNoTrivia);
            descendants = typeDecl.DescendantNodes(descendIntoTrivia: true);
            var endRegions = descendants.OfType<EndRegionDirectiveTriviaSyntax>();
            typeDecl = typeDecl.RemoveNodes(endRegions, SyntaxRemoveOptions.KeepNoTrivia);
            return typeDecl;
        }
    }
}
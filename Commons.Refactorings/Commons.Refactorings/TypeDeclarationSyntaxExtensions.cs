using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Commons.Refactorings
{
    public static class TypeDeclarationSyntaxExtensions
    {
        public static TypeDeclarationSyntax AddPartialModifier(this TypeDeclarationSyntax typeDecl)
        {
            return (typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword)) ? typeDecl : typeDecl.InsertTokensAfter(typeDecl.Modifiers[0], SyntaxFactory.ParseTokens("partial "));
        }

        public static List<TypeDeclarationSyntax> Break(this TypeDeclarationSyntax typeDecl, int membersPerPartial, CancellationToken cancellationToken)
        {
            var partials = new List<TypeDeclarationSyntax>();
            typeDecl = typeDecl.RemoveRegions();
            var allMembers = typeDecl.DescendantNodes().OfType<MemberDeclarationSyntax>().ToList();
            var allMembersCount = allMembers.Count;
            if (allMembersCount <= membersPerPartial) {
                partials.Add(typeDecl);
                return partials;
            }
            typeDecl = typeDecl.AddPartialModifier();
            allMembers = typeDecl.DescendantNodes().OfType<MemberDeclarationSyntax>().ToList();
            allMembersCount = allMembers.Count;
            var first = typeDecl.RemoveMembers(allMembers.Skip(membersPerPartial));
            partials.Add(first.WithoutTrailingTrivia());
            var membersMoved = membersPerPartial;
            while (!(cancellationToken.IsCancellationRequested || membersMoved >= allMembersCount)) {
                var membersToInsert = allMembers.Skip(membersMoved).Take(membersPerPartial).ToArray();
                var tree = SyntaxFactory.ParseSyntaxTree($"{typeDecl.Modifiers.ToString()} class {typeDecl.Identifier} {"{ private int dummy; }"}");
                var nextPartial = tree.GetRoot(cancellationToken).DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                nextPartial = nextPartial.InsertNodesAfter(nextPartial.DescendantNodes().First(), membersToInsert);
                nextPartial = nextPartial.RemoveNode(nextPartial.DescendantNodes().First(), SyntaxRemoveOptions.KeepNoTrivia);
                partials.Add(nextPartial.WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.LineFeed));
                membersMoved += membersPerPartial;
            }            
            return partials;
        }

        private static TypeDeclarationSyntax RemoveRegions(this TypeDeclarationSyntax typeDecl)
        {
            var descendants = typeDecl.DescendantNodes(descendIntoTrivia:true);
            var beginRegions = descendants.OfType<RegionDirectiveTriviaSyntax>();
            typeDecl = typeDecl.RemoveNodes(beginRegions, SyntaxRemoveOptions.KeepNoTrivia);
            descendants = typeDecl.DescendantNodes(descendIntoTrivia: true);
            var endRegions = descendants.OfType<EndRegionDirectiveTriviaSyntax>();
            typeDecl = typeDecl.RemoveNodes(endRegions, SyntaxRemoveOptions.KeepNoTrivia);
            return typeDecl;
        }

        private static TypeDeclarationSyntax RemoveMembers(this TypeDeclarationSyntax typeDecl, IEnumerable<MemberDeclarationSyntax> methods)
        {
            return  typeDecl.RemoveNodes(methods, SyntaxRemoveOptions.KeepNoTrivia);
        }

   }
}
// Commons.Refactorings
//
// Copyright (c) 2016 Rafael 'Monoman' Teixeira, Managed Commons Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

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

        public static TypeDeclarationSyntax AddPartialModifier(this TypeDeclarationSyntax typeDecl) => typeDecl.IsPartial() ? typeDecl : typeDecl.InsertTokensAfter(typeDecl.Modifiers[0], SyntaxFactory.ParseTokens("partial "));

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

        public static List<TypeDeclarationSyntax> Break(this TypeDeclarationSyntax typeDecl, MemberDeclarationSyntax splitMethod, CancellationToken cancellationToken)
        {
            var partials = new List<TypeDeclarationSyntax>();
            typeDecl = typeDecl.RemoveRegions();
            var allMembers = typeDecl.Members.ToList();
            var allMembersCount = allMembers.Count;
            if (allMembersCount <= 1) {
                partials.Add(typeDecl);
                return partials;
            }
            typeDecl = typeDecl.AddPartialModifier();
            allMembers = typeDecl.Members.ToList();
            if (!(cancellationToken.IsCancellationRequested)) {
                var tail = allMembers.SkipWhile(m => !m.IsEquivalentTo(splitMethod)).ToArray();
                var first = typeDecl.RemoveMembers(tail);
                partials.Add(first.WithoutTrailingTrivia());
                var tree = SyntaxFactory.ParseSyntaxTree($"{typeDecl.Modifiers.ToString()} class {typeDecl.Identifier} {"{ private int dummy; }"}");
                var nextPartial = tree.GetRoot(cancellationToken).DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                nextPartial = nextPartial.InsertNodesAfter(nextPartial.DescendantNodes().First(), tail);
                nextPartial = nextPartial.RemoveNode(nextPartial.DescendantNodes().First(), SyntaxRemoveOptions.KeepNoTrivia);
                partials.Add(nextPartial.WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.LineFeed));
            }
            return partials;
        }

        public static string BuildNewPartialSourceName(this TypeDeclarationSyntax typeDecl)
        {
            var methodNode = (MethodDeclarationSyntax)typeDecl.Members.FirstOrDefault(m => m is MethodDeclarationSyntax);
            var suffix = methodNode?.Identifier.Text ?? "Partial";
            return typeDecl.Identifier.Text + '-' + suffix;
        }

        public static IEnumerable<SyntaxNode> FilterSiblingsInNamespace(this TypeDeclarationSyntax typeDecl, Func<SyntaxNode, bool> filter) => (typeDecl.Parent as NamespaceDeclarationSyntax)?.Members.Where(filter) ?? EmptyList;

        public static bool HasManyPartialsInSameSource(this TypeDeclarationSyntax typeDecl) => typeDecl.IsPartial() && CountOfPartialSiblings(typeDecl) > 1;

        public static bool HasTooManyMembers(this TypeDeclarationSyntax typeDecl) => typeDecl.Members.Count > MaximumNumberOfMembers;

        static List<SyntaxNode> EmptyList = new List<SyntaxNode>();

        static int CountOfPartialSiblings(TypeDeclarationSyntax typeDecl) => typeDecl.FilterSiblingsInNamespace(n => HasSameName(n, typeDecl)).Count();

        static bool HasSameName(SyntaxNode node, TypeDeclarationSyntax typeDecl)
        {
            var typeDeclarationSyntax = (node as TypeDeclarationSyntax);
            return typeDeclarationSyntax.IsPartial() && typeDeclarationSyntax?.Identifier.Text == typeDecl.Identifier.Text;
        }

        static bool IsPartial(this TypeDeclarationSyntax typeDecl)
        {
            if (typeDecl == null)
                return false;
            return typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
        }

        static TypeDeclarationSyntax RemoveMembers(this TypeDeclarationSyntax typeDecl, IEnumerable<MemberDeclarationSyntax> methods) => typeDecl.RemoveNodes(methods, SyntaxRemoveOptions.KeepNoTrivia);

        static TypeDeclarationSyntax RemoveRegions(this TypeDeclarationSyntax typeDecl)
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

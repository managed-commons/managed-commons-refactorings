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
    public static class ClassDeclarationSyntaxExtensions
    {
        public const int MaximumNumberOfMembers = 12;

        public static ClassDeclarationSyntax AddPartialModifier(this ClassDeclarationSyntax classDecl)
            => classDecl.IsPartial() ? classDecl : classDecl.InsertPartialFragment();

        public static async Task<SyntaxNode> AsNewCompilationUnitAsync(this ClassDeclarationSyntax classDecl, Document document, CancellationToken cancellationToken)
        {
            var newRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var nodesToRemove = classDecl.FilterSiblingsInNamespace(n => !n.Equals(classDecl));
            var cleanedRoot = newRoot.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia);
            return Formatter.Format(cleanedRoot, document.Project.Solution.Workspace, cancellationToken: cancellationToken);
        }

        public static List<ClassDeclarationSyntax> Break(this ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var partials = new List<ClassDeclarationSyntax>();
            classDecl = classDecl.RemoveRegions();
            var allMembers = classDecl.Members.ToList();
            var allMembersCount = allMembers.Count;
            if (allMembersCount <= MaximumNumberOfMembers)
            {
                partials.Add(classDecl);
                return partials;
            }
            classDecl = classDecl.AddPartialModifier();
            allMembers = classDecl.Members.ToList();
            allMembersCount = allMembers.Count;
            var first = classDecl.RemoveMembers(allMembers.Skip(MaximumNumberOfMembers));
            partials.Add(first.WithoutTrailingTrivia());
            var membersMoved = MaximumNumberOfMembers;
            while (!(cancellationToken.IsCancellationRequested || membersMoved >= allMembersCount))
            {
                var membersToInsert = allMembers.Skip(membersMoved).Take(MaximumNumberOfMembers).ToArray();
                var tree = SyntaxFactory.ParseSyntaxTree($"{classDecl.Modifiers} class {classDecl.Identifier} {CLASS_BODY_TEMPLATE}");
                var nextPartial = tree.GetRoot(cancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                nextPartial = nextPartial.InsertNodesAfter(nextPartial.DescendantNodes().First(), membersToInsert);
                nextPartial = nextPartial.RemoveNode(nextPartial.DescendantNodes().First(), SyntaxRemoveOptions.KeepNoTrivia);
                partials.Add(nextPartial.WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.LineFeed));
                membersMoved += MaximumNumberOfMembers;
            }
            return partials;
        }

        public static List<ClassDeclarationSyntax> Break(this ClassDeclarationSyntax classDecl, MemberDeclarationSyntax splitMethod, CancellationToken cancellationToken)
        {
            try
            {
                var partials = new List<ClassDeclarationSyntax>();
                classDecl = classDecl.RemoveRegions();
                var allMembers = classDecl.Members.ToList();
                var allMembersCount = allMembers.Count;
                if (allMembersCount <= 1)
                {
                    partials.Add(classDecl);
                    return partials;
                }
                classDecl = classDecl.AddPartialModifier();
                allMembers = classDecl.Members.ToList();
                if (!(cancellationToken.IsCancellationRequested))
                {
                    var tail = allMembers.SkipWhile(m => !m.IsEquivalentTo(splitMethod)).ToArray();
                    var first = classDecl.RemoveMembers(tail);
                    partials.Add(first.WithoutTrailingTrivia());
                    var tree = SyntaxFactory.ParseSyntaxTree($"{classDecl.Modifiers} class {classDecl.Identifier} {CLASS_BODY_TEMPLATE}");
                    var nextPartial = tree.GetRoot(cancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                    nextPartial = nextPartial.InsertNodesAfter(nextPartial.DescendantNodes().First(), tail);
                    nextPartial = nextPartial.RemoveNode(nextPartial.DescendantNodes().First(), SyntaxRemoveOptions.KeepNoTrivia);
                    partials.Add(nextPartial.WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.LineFeed));
                }
                return partials;
            } catch (Exception)
            {
                var partials = new List<ClassDeclarationSyntax>();
                partials.Add(classDecl);
                return partials;
            }
        }

        public static string BuildNewPartialSourceName(this ClassDeclarationSyntax classDecl)
        {
            var methodNode = (MethodDeclarationSyntax)classDecl.Members.FirstOrDefault(m => m is MethodDeclarationSyntax);
            var suffix = methodNode?.Identifier.Text ?? "Partial";
            return classDecl.Identifier.Text + '-' + suffix;
        }

        public static IEnumerable<SyntaxNode> FilterSiblingsInNamespace(this ClassDeclarationSyntax classDecl, Func<SyntaxNode, bool> filter) => (classDecl.Parent as NamespaceDeclarationSyntax)?.Members.Where(filter) ?? EmptyList;

        public static bool HasManyPartialsInSameSource(this ClassDeclarationSyntax classDecl) => classDecl.IsPartial() && CountOfPartialSiblings(classDecl) > 1;

        public static bool HasTooManyMembers(this ClassDeclarationSyntax classDecl) => classDecl.Members.Count > MaximumNumberOfMembers;

        private static readonly string CLASS_BODY_TEMPLATE = @"
{private int dummy;}

";

        private static readonly List<SyntaxNode> EmptyList = new List<SyntaxNode>();

        private static int CountOfPartialSiblings(ClassDeclarationSyntax classDecl) => classDecl.FilterSiblingsInNamespace(n => HasSameName(n, classDecl)).Count();

        private static bool HasSameName(SyntaxNode node, ClassDeclarationSyntax classDecl)
        {
            var typeDeclarationSyntax = node as ClassDeclarationSyntax;
            return typeDeclarationSyntax.IsPartial() && typeDeclarationSyntax?.Identifier.Text == classDecl.Identifier.Text;
        }

        private static ClassDeclarationSyntax InsertPartialFragment(this ClassDeclarationSyntax classDecl)
            => classDecl.AddModifiers(SyntaxFactory.ParseTokens("partial").First());

        private static bool IsPartial(this ClassDeclarationSyntax classDecl)
        {
            if (classDecl == null)
                return false;
            return classDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
        }

        private static ClassDeclarationSyntax RemoveDescendantsOfType<T>(this ClassDeclarationSyntax classDecl) where T : SyntaxNode
        {
            if (classDecl == null)
                return classDecl;
            var nodesToRemove = classDecl.DescendantNodes(descendIntoTrivia: true).OfType<T>();
            return nodesToRemove.Any() ? classDecl.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia) : classDecl;
        }

        private static ClassDeclarationSyntax RemoveMembers(this ClassDeclarationSyntax classDecl, IEnumerable<MemberDeclarationSyntax> methods)
            => classDecl.RemoveNodes(methods, SyntaxRemoveOptions.KeepNoTrivia);

        private static ClassDeclarationSyntax RemoveRegions(this ClassDeclarationSyntax classDecl)
            => classDecl.RemoveDescendantsOfType<RegionDirectiveTriviaSyntax>().RemoveDescendantsOfType<EndRegionDirectiveTriviaSyntax>();
    }
}

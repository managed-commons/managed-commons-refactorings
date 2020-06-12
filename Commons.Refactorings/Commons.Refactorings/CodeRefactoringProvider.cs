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
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Commons.Refactorings
{
	[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(CodeRefactoringProvider)), Shared]
	public class CodeRefactoringProvider : Microsoft.CodeAnalysis.CodeRefactorings.CodeRefactoringProvider
	{
		public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			// Find the node at the selection.
			var node = root.FindNode(context.Span);

			var classDecl = node as ClassDeclarationSyntax;
			if (classDecl == null || IsMemberOfSomeClass(classDecl))
			{
				var memberDecl = node as MemberDeclarationSyntax;
				if (memberDecl != null && IsMemberOfSomeClass(memberDecl))
				{
					var action = CodeAction.Create("Split here to a partial", c => Action_BreakIntoPartialFromMemberAsync(context.Document, memberDecl, c));
					context.RegisterRefactoring(action);
				}
				return;
			}
			if (classDecl.HasTooManyMembers())
			{
				var action = CodeAction.Create("Break into partials", c => Action_BreakIntoPartialsAsync(context.Document, classDecl, c));
				context.RegisterRefactoring(action);
			}
			if (classDecl.HasManyPartialsInSameSource())
			{
				var action = CodeAction.Create("Move partial to new source file", c => Action_SeparatePartialAsync(context.Document, classDecl, c));
				context.RegisterRefactoring(action);
			}
			if (classDecl.HasManyInSameSource())
			{
				var action = CodeAction.Create("Move class to new source file", c => Action_SeparateClassAsync(context.Document, classDecl, c));
				context.RegisterRefactoring(action);
			}
		}

		private async static Task<Solution> Action_BreakIntoPartialFromMemberAsync(Document document, MemberDeclarationSyntax memberDecl, CancellationToken cancellationToken)
		{
			var classDecl = FindParentClassDeclaration(memberDecl);
			var partials = classDecl.Break(memberDecl, cancellationToken);
			return await document.Update(cancellationToken,
				root =>
				{
					var newRoot = root.ReplaceNode(classDecl, partials.First());
					var classDeclarations = newRoot.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().Where(n => n.Identifier.Text == classDecl.Identifier.Text);
					var newTypeDecl = classDeclarations.FirstOrDefault(n => n.SpanStart >= classDecl.SpanStart) ?? classDeclarations.FirstOrDefault();
					var rootNode = newTypeDecl == null ? newRoot : newRoot.InsertNodesAfter(newTypeDecl, partials.Skip(1));
					return Formatter.Format(rootNode, document.Project.Solution.Workspace, cancellationToken: cancellationToken);
				});
		}

		private async static Task<Solution> Action_BreakIntoPartialsAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
		{
			var partials = classDecl.Break(cancellationToken);
			return await document.Update(cancellationToken,
				root =>
				{
					var newRoot = root.ReplaceNode(classDecl, partials.First());
					var classDeclarations = newRoot.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>();
					var newTypeDecl = classDeclarations.FirstOrDefault(n => n.Identifier.Text == classDecl.Identifier.Text);
					var rootNode = newTypeDecl == null ? newRoot : newRoot.InsertNodesAfter(newTypeDecl, partials.Skip(1));
					return Formatter.Format(rootNode, document.Project.Solution.Workspace, cancellationToken: cancellationToken);
				});
		}

		private async static Task<Solution> Action_SeparateClassAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
		{
			return await ActionInternal_SeparateClassAsync(document, classDecl, cancellationToken, classDecl.Identifier.Text);
		}

		private async static Task<Solution> Action_SeparatePartialAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
		{
			return await ActionInternal_SeparateClassAsync(document, classDecl, cancellationToken, classDecl.BuildNewPartialSourceName());
		}

		private static async Task<Solution> ActionInternal_SeparateClassAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken, string newSourceName)
		{
			var newCompilationUnit = await classDecl.AsNewCompilationUnitAsync(document, cancellationToken);
			if (newCompilationUnit == null || cancellationToken.IsCancellationRequested)
				return document.Project.Solution;
			var newSolution = await document.Update(cancellationToken,
				root =>
				{
					var newRoot = root.RemoveNode(classDecl, SyntaxRemoveOptions.KeepNoTrivia);
					return Formatter.Format(newRoot, document.Project.Solution.Workspace, cancellationToken: cancellationToken);
				});
			if (cancellationToken.IsCancellationRequested)
				return document.Project.Solution;
			return document.AddDerivedDocument(newSolution, newSourceName, newCompilationUnit);
		}

		private static T FindParent<T>(SyntaxNode node) where T : SyntaxNode
		{
			if (node == null)
				return null;
			if (node is T)
				return (T)node;
			return FindParent<T>(node.Parent);
		}

		private static ClassDeclarationSyntax FindParentClassDeclaration(MemberDeclarationSyntax memberDecl)
			=> FindParent<ClassDeclarationSyntax>(memberDecl.Parent);

		private static bool IsMemberOfSomeClass(MemberDeclarationSyntax memberDecl)
			=> FindParentClassDeclaration(memberDecl) != null;
	}
}

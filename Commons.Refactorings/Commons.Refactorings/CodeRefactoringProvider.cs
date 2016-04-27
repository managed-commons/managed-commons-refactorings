using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Commons.Refactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(CodeRefactoringProvider)), Shared]
    internal class CodeRefactoringProvider : Microsoft.CodeAnalysis.CodeRefactorings.CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(context.Span);

            // Only offer a refactoring if the selected node is a type declaration node.
            var typeDecl = node as TypeDeclarationSyntax;
            if (typeDecl == null) {
                return;
            }
            if (!typeDecl.Keyword.IsKind(SyntaxKind.ClassKeyword))
                return;
            if (typeDecl.HasTooManyMembers()) {
                var action = CodeAction.Create("Break into partials", c => Action_BreakIntoPartials(context.Document, typeDecl, c));
                context.RegisterRefactoring(action);
            }
            if (typeDecl.HasManyPartialsInSameSource()) {
                var action = CodeAction.Create("Move partial to new source file", c => Action_SeparatePartial(context.Document, typeDecl, c));
                context.RegisterRefactoring(action);
            }
        }

        private async Task<Solution> Action_BreakIntoPartials(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            List<TypeDeclarationSyntax> partials = typeDecl.Break(cancellationToken);
            return await document.Update(cancellationToken,
                root => {
                    var newRoot = root.ReplaceNode(typeDecl, partials.First());
                    var classDeclarations = newRoot.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>();
                    var newTypeDecl = classDeclarations.FirstOrDefault(n => n.Identifier.Text == typeDecl.Identifier.Text);
                    var rootNode = newTypeDecl == null ? newRoot : newRoot.InsertNodesAfter(newTypeDecl, partials.Skip(1));
                    return Formatter.Format(rootNode, document.Project.Solution.Workspace, cancellationToken: cancellationToken);
                });
        }

        private async Task<Solution> Action_SeparatePartial(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var newCompilationUnit = await typeDecl.AsNewCompilationUnit(document, cancellationToken);
            if (newCompilationUnit == null || cancellationToken.IsCancellationRequested)
                return document.Project.Solution;
            var newSourceName = typeDecl.BuildNewPartialSourceName();
            var newSolution = await document.Update(cancellationToken,
                root => {
                    var newRoot = root.RemoveNode(typeDecl, SyntaxRemoveOptions.KeepNoTrivia);
                    return Formatter.Format(newRoot, document.Project.Solution.Workspace, cancellationToken: cancellationToken);
                });
            if (cancellationToken.IsCancellationRequested)
                return document.Project.Solution;
            return document.AddDerivedDocument(newSolution, newSourceName, newCompilationUnit);
        }

    }
}
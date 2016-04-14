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
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(CommonsRefactoringsCodeRefactoringProvider)), Shared]
    internal class CommonsRefactoringsCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            // TODO: Replace the following code with your own analysis, generating a CodeAction for each refactoring to offer
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
            if (typeDecl.Members.Count(m => m.IsKind(SyntaxKind.MethodDeclaration)) <= MaximumNumberOfMembers)
                return;
            // For any type declaration node, create a code action to reverse the identifier text.
            var action = CodeAction.Create("Break into partials", c => BreakIntoPartials(context.Document, typeDecl, c));
            // Register this code action.
            context.RegisterRefactoring(action);
        }

        private const int MaximumNumberOfMembers = 12;

        private async Task<Solution> BreakIntoPartials(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            List<TypeDeclarationSyntax> partials = typeDecl.Break(MaximumNumberOfMembers, cancellationToken);
            return await document.Update(cancellationToken,
                root => {
                    var newRoot = root.ReplaceNode(typeDecl, partials.First());
                    var classDeclarations = newRoot.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>();
                    var newTypeDecl = classDeclarations.FirstOrDefault(n => n.Identifier.Text == typeDecl.Identifier.Text);
                    var rootNode = newTypeDecl == null ? newRoot : newRoot.InsertNodesAfter(newTypeDecl, partials.Skip(1));
                    return Formatter.Format(rootNode, document.Project.Solution.Workspace, cancellationToken:cancellationToken);
                });
        }
    }
}
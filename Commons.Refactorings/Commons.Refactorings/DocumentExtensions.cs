using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Commons.Refactorings
{
    public static class DocumentExtensions
    {
        public static Solution AddDerivedDocument(this Document document, Solution solution, string newSourceName, SyntaxNode newCompilationUnit)
        {
            return solution.AddDocument(document.CreateNewId(), newSourceName, newCompilationUnit, document.Folders());
        }

        public static async Task<Solution> Update(this Document document, CancellationToken cancellationToken, Func<SyntaxNode, SyntaxNode> transform)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = transform(root);
            var newDocument = document.WithSyntaxRoot(newRoot);
            var text = await newDocument.GetTextAsync(cancellationToken);
            return document.Project.Solution.WithDocumentText(document.Id, text);
        }

        static DocumentId CreateNewId(this Document document)
        {
            return DocumentId.CreateNewId(document.Project.Id);
        }

        static List<string> Folders(this Document document)
        {
            var documentFolderPath = Path.GetDirectoryName(document.FilePath);
            var projectFolderPath = Path.GetDirectoryName(document.Project.FilePath);
            var folders = documentFolderPath.Split('\\', '/').ToList();
            foreach (var folder in projectFolderPath.Split('\\', '/'))
                folders.Remove(folder);
            return folders;
        }
    }
}
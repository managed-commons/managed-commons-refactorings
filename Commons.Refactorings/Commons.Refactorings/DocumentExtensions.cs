using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Commons.Refactorings
{
    public static class DocumentExtensions
    {
        public static async Task<Solution> Update(this Document document, CancellationToken cancellationToken, Func<SyntaxNode, SyntaxNode> transform)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = transform(root);
            var newDocument = document.WithSyntaxRoot(newRoot);
            var text = await newDocument.GetTextAsync(cancellationToken);
            return document.Project.Solution.WithDocumentText(document.Id, text);
        }
    }
}

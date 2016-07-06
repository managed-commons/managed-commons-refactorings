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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Commons.Refactorings
{
    public static class DocumentExtensions
    {
        public static Solution AddDerivedDocument(this Document document, Solution solution, string newSourceName, SyntaxNode newCompilationUnit) => solution.AddDocument(document.CreateNewId(), newSourceName, newCompilationUnit, document.Folders());

        public static async Task<Solution> Update(this Document document, CancellationToken cancellationToken, Func<SyntaxNode, SyntaxNode> transform)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = transform(root);
            var newDocument = document.WithSyntaxRoot(newRoot);
            var text = await newDocument.GetTextAsync(cancellationToken);
            return document.Project.Solution.WithDocumentText(document.Id, text);
        }

        static DocumentId CreateNewId(this Document document) => DocumentId.CreateNewId(document.Project.Id);

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

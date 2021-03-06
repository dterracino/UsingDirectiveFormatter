﻿namespace Microsoft.VisualStudio.Text
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using UsingDirectiveFormatter.Commands;
    using UsingDirectiveFormatter.Utilities;

    /// <summary>
    /// TextBufferExtensions
    /// </summary>
    public static class TextBufferExtensions
    {
        /// <summary>
        /// The using namespace directive prefix
        /// </summary>
        private static readonly string UsingNamespaceDirectivePrefix = "using";

        /// <summary>
        /// The namespace declaration prefix
        /// </summary>
        private static readonly string NamespaceDeclarationPrefix = "namespace";

        /// <summary>
        /// Formats the specified buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        public static void Format(this ITextBuffer buffer, IList<SortStandard> sortStandards, 
            bool insideNamespace = true)
        {
            ArgumentGuard.ArgumentNotNull(buffer, "buffer");

            var snapShot = buffer.CurrentSnapshot;

            int cursor = 0;
            int tail = 0;

            // using directive related
            var usingDirectives = new List<string>();
            int nsInnerStartPos = 0;
            int nsOuterStartPos = 0;

            // Namespace related flags
            bool nsReached = false;
            int startPos = 0;

            string indent = "";

            // Using directives before namespace (if any)
            Span? prensSpan = null;
            // Using directives inside namespace, or all usings is there's no namespace
            Span? nsSpan = null;

            bool lastLineEmptyOrComment = false;
            int spanToPreserve = 0;

            foreach (var line in snapShot.Lines)
            {
                var lineText = line.GetText();
                var lineTextTrimmed = lineText.TrimStart();

                cursor = tail;
                tail += line.LengthIncludingLineBreak;

                if (nsReached && insideNamespace &&
                    !string.IsNullOrWhiteSpace(lineTextTrimmed) && 
                    string.IsNullOrEmpty(indent))
                {
                    indent = lineText.Substring(0, lineText.IndexOf(lineTextTrimmed));
                }

                if (string.IsNullOrWhiteSpace(lineTextTrimmed) ||
                        lineTextTrimmed.StartsWith("/", StringComparison.Ordinal))
                {
                    spanToPreserve += line.LengthIncludingLineBreak;
                    lastLineEmptyOrComment = true;
                }
                else
                {
                    if (!lastLineEmptyOrComment)
                    {
                        spanToPreserve = 0;
                    }

                    lastLineEmptyOrComment = false;

                    if (lineTextTrimmed.StartsWith(UsingNamespaceDirectivePrefix, StringComparison.Ordinal))
                    {
                        if (nsInnerStartPos == 0)
                        {
                            nsInnerStartPos = cursor;
                        }

                        if (startPos == 0)
                        {
                            startPos = cursor;
                        }

                        if (nsOuterStartPos == 0 && !nsReached)
                        {
                            nsOuterStartPos = cursor;
                        }

                        usingDirectives.Add(lineTextTrimmed);
                    }
                    else if (lineTextTrimmed.StartsWith(NamespaceDeclarationPrefix, StringComparison.Ordinal))
                    {
                        prensSpan = new Span(0, cursor - spanToPreserve);
                        nsReached = true;
                        nsInnerStartPos = tail;
                        startPos = tail;
                        nsOuterStartPos = cursor;
                    }
                    else if (lineTextTrimmed.Equals("{", StringComparison.Ordinal))
                    {
                        if (nsReached)
                        {
                            nsInnerStartPos = tail;
                            startPos = tail;
                        }
                    }
                    else if (lineTextTrimmed.Equals(";", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    else
                    {
                        nsSpan =
                            new Span(startPos, cursor - startPos - spanToPreserve);
                        break;
                    }
                }
            }

            usingDirectives = usingDirectives.OrderBySortStandards(sortStandards)
                .ToList()
                .Select(s => indent + s)
                .ToList();

            var insertPos = nsReached && insideNamespace ? nsInnerStartPos : nsOuterStartPos;
            var insertString = string.Join("\r\n", usingDirectives) + "\r\n";

            // Testing
            var edit = buffer.CreateEdit();
            edit.Insert(insertPos, insertString);
            if (nsSpan != null)
            {
                edit.Delete(nsSpan.Value);
            }
            if (prensSpan != null)
            {
                edit.Delete(prensSpan.Value);
            }
            edit.Apply();
        }
    }
}

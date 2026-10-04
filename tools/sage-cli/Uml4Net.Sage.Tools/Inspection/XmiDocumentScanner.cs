// -------------------------------------------------------------------------------------------------
// <copyright file="XmiDocumentScanner.cs" company="Starion Group S.A.">
//
//   Copyright (C) 2019-2026 Starion Group S.A.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace Uml4Net.Sage.Tools.Inspection
{
    using System;
    using System.Collections.Generic;
    using System.Xml;

    /// <summary>
    /// A UML element declared in the inspected document: its metaclass (from <c>xmi:type</c>, or the element
    /// name for a root element such as <c>&lt;uml:Package&gt;</c>), its <c>xmi:id</c>, and where it starts.
    /// </summary>
    /// <param name="Metaclass">The UML metaclass name, e.g. <c>Class</c>.</param>
    /// <param name="XmiId">The element's <c>xmi:id</c>, if it has one.</param>
    /// <param name="Line">The 1-based line the element starts on.</param>
    public sealed record ScannedElement(string Metaclass, string? XmiId, int Line);

    /// <summary>
    /// What a plain XML pass over the inspected document finds - independent of whether <c>uml4net.xmi</c> can
    /// read it, so a file the reader aborts on (e.g. because of an abstract <c>xmi:type</c>) still gets
    /// line-accurate findings.
    /// </summary>
    /// <param name="Elements">Every UML element declared (not merely referenced through an <c>href</c>), in document order.</param>
    /// <param name="XmiIdLines">Every <c>xmi:id</c> with the line(s) declaring it; more than one line means a duplicate.</param>
    /// <param name="PathmapDocuments">Every <c>pathmap://</c> document referenced through an <c>href</c> (without the fragment), sorted.</param>
    public sealed record XmiDocumentScan(
        IReadOnlyList<ScannedElement> Elements,
        IReadOnlyDictionary<string, IReadOnlyList<int>> XmiIdLines,
        IReadOnlyList<string> PathmapDocuments);

    /// <summary>
    /// Scans an XMI document with an <see cref="XmlReader"/> for <see cref="XmiDocumentScan"/>.
    /// </summary>
    public static class XmiDocumentScanner
    {
        private const string PathmapScheme = "pathmap://";

        /// <summary>
        /// Scans the document at <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The XMI document to scan.</param>
        /// <returns>The scan result.</returns>
        public static XmiDocumentScan Scan(string path)
        {
            var elements = new List<ScannedElement>();
            var xmiIdLines = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var pathmapDocuments = new SortedSet<string>(StringComparer.Ordinal);

            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            var lineInfo = (IXmlLineInfo)reader;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                var line = lineInfo.LineNumber;
                var elementIsUml = IsUmlNamespace(reader.NamespaceURI);
                var elementLocalName = reader.LocalName;
                string? xmiId = null;
                string? xmiType = null;
                string? href = null;

                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        if (IsXmiNamespace(reader.NamespaceURI) && reader.LocalName == "id")
                        {
                            xmiId = reader.Value;
                        }
                        else if (IsXmiNamespace(reader.NamespaceURI) && reader.LocalName == "type")
                        {
                            xmiType = reader.Value;
                        }
                        else if (reader.LocalName == "href" && reader.NamespaceURI.Length == 0)
                        {
                            href = reader.Value;
                        }
                    }
                    while (reader.MoveToNextAttribute());

                    reader.MoveToElement();
                }

                if (xmiId is not null)
                {
                    if (!xmiIdLines.TryGetValue(xmiId, out var lines))
                    {
                        lines = [];
                        xmiIdLines[xmiId] = lines;
                    }

                    lines.Add(line);
                }

                if (href is not null)
                {
                    if (href.StartsWith(PathmapScheme, StringComparison.Ordinal))
                    {
                        var fragment = href.IndexOf('#');
                        pathmapDocuments.Add(fragment < 0 ? href : href[..fragment]);
                    }

                    // a proxy for an element declared elsewhere, not a declaration
                    continue;
                }

                var metaclass = MetaclassOf(reader, xmiType, elementIsUml, elementLocalName);
                if (metaclass is not null)
                {
                    elements.Add(new ScannedElement(metaclass, xmiId, line));
                }
            }

            var readOnlyLines = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
            foreach (var (id, lines) in xmiIdLines)
            {
                readOnlyLines[id] = lines;
            }

            return new XmiDocumentScan(elements, readOnlyLines, [.. pathmapDocuments]);
        }

        private static string? MetaclassOf(XmlReader reader, string? xmiType, bool elementIsUml, string elementLocalName)
        {
            if (xmiType is not null)
            {
                var separator = xmiType.IndexOf(':');
                var prefix = separator < 0 ? string.Empty : xmiType[..separator];
                var namespaceUri = reader.LookupNamespace(prefix);
                return namespaceUri is not null && IsUmlNamespace(namespaceUri) ? xmiType[(separator + 1)..] : null;
            }

            return elementIsUml ? elementLocalName : null;
        }

        private static bool IsXmiNamespace(string namespaceUri)
        {
            return namespaceUri.Contains("/XMI", StringComparison.Ordinal);
        }

        private static bool IsUmlNamespace(string namespaceUri)
        {
            return namespaceUri.StartsWith("http://www.omg.org/spec/UML/", StringComparison.Ordinal)
                || namespaceUri.StartsWith("http://www.eclipse.org/uml2/", StringComparison.Ordinal);
        }
    }
}

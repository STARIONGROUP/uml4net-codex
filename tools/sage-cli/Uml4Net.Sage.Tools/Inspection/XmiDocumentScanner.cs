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
                var attributes = ReadAttributes(reader);

                if (attributes.XmiId is not null)
                {
                    RecordXmiId(xmiIdLines, attributes.XmiId, line);
                }

                if (attributes.Href is not null)
                {
                    // a proxy for an element declared elsewhere, not a declaration
                    RecordPathmapDocument(pathmapDocuments, attributes.Href);
                    continue;
                }

                var metaclass = MetaclassOf(reader, attributes.XmiType);
                if (metaclass is not null)
                {
                    elements.Add(new ScannedElement(metaclass, attributes.XmiId, line));
                }
            }

            var readOnlyLines = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
            foreach (var (id, lines) in xmiIdLines)
            {
                readOnlyLines[id] = lines;
            }

            return new XmiDocumentScan(elements, readOnlyLines, [.. pathmapDocuments]);
        }

        /// <summary>
        /// Reads the <c>xmi:id</c>, <c>xmi:type</c> and <c>href</c> attributes of the element <paramref name="reader"/>
        /// is positioned on, leaving it positioned on that element.
        /// </summary>
        private static (string? XmiId, string? XmiType, string? Href) ReadAttributes(XmlReader reader)
        {
            string? xmiId = null;
            string? xmiType = null;
            string? href = null;

            if (!reader.MoveToFirstAttribute())
            {
                return (xmiId, xmiType, href);
            }

            do
            {
                switch (reader.LocalName)
                {
                    case "id" when IsXmiNamespace(reader.NamespaceURI):
                        xmiId = reader.Value;
                        break;
                    case "type" when IsXmiNamespace(reader.NamespaceURI):
                        xmiType = reader.Value;
                        break;
                    case "href" when reader.NamespaceURI.Length == 0:
                        href = reader.Value;
                        break;
                }
            }
            while (reader.MoveToNextAttribute());

            reader.MoveToElement();
            return (xmiId, xmiType, href);
        }

        private static void RecordXmiId(Dictionary<string, List<int>> xmiIdLines, string xmiId, int line)
        {
            if (!xmiIdLines.TryGetValue(xmiId, out var lines))
            {
                lines = [];
                xmiIdLines[xmiId] = lines;
            }

            lines.Add(line);
        }

        private static void RecordPathmapDocument(SortedSet<string> pathmapDocuments, string href)
        {
            if (!href.StartsWith(PathmapScheme, StringComparison.Ordinal))
            {
                return;
            }

            var fragment = href.IndexOf('#');
            pathmapDocuments.Add(fragment < 0 ? href : href[..fragment]);
        }

        /// <summary>
        /// Gets the UML metaclass the element <paramref name="reader"/> is positioned on declares: the local part of its
        /// <c>xmi:type</c> when that is in a UML namespace, otherwise - for an element without <c>xmi:type</c>, such as a
        /// root <c>&lt;uml:Package&gt;</c> - its own name when the element itself is in a UML namespace.
        /// </summary>
        private static string? MetaclassOf(XmlReader reader, string? xmiType)
        {
            if (xmiType is null)
            {
                return IsUmlNamespace(reader.NamespaceURI) ? reader.LocalName : null;
            }

            var separator = xmiType.IndexOf(':');
            var prefix = separator < 0 ? string.Empty : xmiType[..separator];
            var namespaceUri = reader.LookupNamespace(prefix);
            return namespaceUri is not null && IsUmlNamespace(namespaceUri) ? xmiType[(separator + 1)..] : null;
        }

        private static bool IsXmiNamespace(string namespaceUri)
        {
            return namespaceUri.Contains("/XMI", StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets whether <paramref name="namespaceUri"/> is an OMG UML or Eclipse UML2 namespace. Namespace URIs are
        /// identifiers, not locations, so only their host and path are compared, never fetched.
        /// </summary>
        private static bool IsUmlNamespace(string namespaceUri)
        {
            return namespaceUri.Contains("www.omg.org/spec/UML/", StringComparison.Ordinal)
                || namespaceUri.Contains("www.eclipse.org/uml2/", StringComparison.Ordinal);
        }
    }
}

// -------------------------------------------------------------------------------------------------
// <copyright file="MetamodelIndexGenerator.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.MetamodelGen.Generators
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;

    using Uml4Net.Sage.MetamodelGen;
    using Uml4Net.Sage.MetamodelGen.Model;

    /// <summary>
    /// Builds <c>metamodel/index.json</c> (an array of rows, for Frictionless tabular-resource validity)
    /// and <c>metamodel/index.md</c> (the same, grouped by package, for human/grep use).
    /// </summary>
    public static class MetamodelIndexGenerator
    {
        /// <summary>
        /// Builds the sorted index rows for every class, enumeration and primitive type in the catalog.
        /// </summary>
        public static IReadOnlyList<MetamodelIndexRow> BuildRows(ElementCatalog catalog)
        {
            var rows = new List<MetamodelIndexRow>();

            rows.AddRange(catalog.Classes
                .Where(c => !string.IsNullOrEmpty(c.QualifiedName))
                .Select(c => new MetamodelIndexRow(c.Name, "class", ElementNames.NamespaceOf(c), c.QualifiedName, c.IsAbstract, $"elements/{c.Name}.md")));

            rows.AddRange(catalog.Enumerations
                .Where(e => !string.IsNullOrEmpty(e.QualifiedName))
                .Select(e => new MetamodelIndexRow(e.Name, "enumeration", ElementNames.NamespaceOf(e), e.QualifiedName, false, $"elements/{e.Name}.md")));

            rows.AddRange(catalog.PrimitiveTypes
                .Where(p => !string.IsNullOrEmpty(p.QualifiedName))
                .Select(p => new MetamodelIndexRow(p.Name, "primitiveType", ElementNames.NamespaceOf(p), p.QualifiedName, false, $"elements/{p.Name}.md")));

            rows.AddRange(catalog.Associations
                .Where(a => !string.IsNullOrEmpty(a.QualifiedName))
                .Select(a => new MetamodelIndexRow(a.Name, "association", ElementNames.NamespaceOf(a), a.QualifiedName, a.IsAbstract, $"elements/{a.Name}.md")));

            return rows.OrderBy(row => row.QualifiedName, System.StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Serializes the rows as JSON (an array, per the tabular-resource convention).
        /// </summary>
        public static string SerializeJson(IReadOnlyList<MetamodelIndexRow> rows)
        {
            return JsonSerializer.Serialize(rows, MetamodelJsonGenerator.SerializerOptions);
        }

        /// <summary>
        /// Renders the human-readable markdown table, grouped by package.
        /// </summary>
        public static string RenderMarkdown(IReadOnlyList<MetamodelIndexRow> rows)
        {
            var builder = new StringBuilder();
            builder.Append("# Metamodel index\n\n");

            foreach (var group in rows.GroupBy(row => row.Package).OrderBy(g => g.Key, System.StringComparer.Ordinal))
            {
                builder.Append("## ").Append(string.IsNullOrEmpty(group.Key) ? "(root)" : group.Key).Append("\n\n");
                builder.Append("| Name | Kind | Abstract | File |\n|---|---|---|---|\n");

                foreach (var row in group.OrderBy(r => r.Name, System.StringComparer.Ordinal))
                {
                    builder.Append("| ").Append(row.Name).Append(" | ").Append(row.Kind).Append(" | ")
                        .Append(row.IsAbstract ? "yes" : "").Append(" | [").Append(row.File).Append("](").Append(row.File).Append(") |\n");
                }

                builder.Append('\n');
            }

            return builder.ToString().TrimEnd('\n') + "\n";
        }
    }
}

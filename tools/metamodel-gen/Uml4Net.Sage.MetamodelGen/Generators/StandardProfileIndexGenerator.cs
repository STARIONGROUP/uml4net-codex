// -------------------------------------------------------------------------------------------------
// <copyright file="StandardProfileIndexGenerator.cs" company="Starion Group S.A.">
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
    /// Builds <c>standard-profile/index.json</c> and <c>standard-profile/index.md</c> for every Standard
    /// Profile stereotype in the catalog.
    /// </summary>
    public static class StandardProfileIndexGenerator
    {
        /// <summary>
        /// Builds the sorted index rows for every stereotype in the catalog.
        /// </summary>
        public static IReadOnlyList<StandardProfileIndexRow> BuildRows(ElementCatalog catalog)
        {
            return catalog.Stereotypes
                .Where(s => !string.IsNullOrEmpty(s.QualifiedName))
                .Select(s => new StandardProfileIndexRow(
                    s.QualifiedName,
                    "stereotype",
                    $"pages/{s.Name}.md",
                    "StandardProfile",
                    StereotypeFileGenerator.ProfileOf(s),
                    StereotypeFileGenerator.ExtensionsOf(s).Select(extension => extension.Metaclass).ToList()))
                .OrderBy(row => row.QualifiedName, System.StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Serializes the rows as JSON (an array, per the tabular-resource convention).
        /// </summary>
        public static string SerializeJson(IReadOnlyList<StandardProfileIndexRow> rows)
        {
            return JsonSerializer.Serialize(rows, MetamodelJsonGenerator.SerializerOptions);
        }

        /// <summary>
        /// Renders the human-readable markdown table.
        /// </summary>
        public static string RenderMarkdown(IReadOnlyList<StandardProfileIndexRow> rows)
        {
            var builder = new StringBuilder();
            builder.Append("# Standard Profile index\n\n");
            builder.Append("| Qualified name | Kind | Base metaclasses | File | Source |\n|---|---|---|---|---|\n");

            foreach (var row in rows)
            {
                builder.Append("| ").Append(row.QualifiedName).Append(" | ").Append(row.Kind)
                    .Append(" | ").Append(string.Join(", ", row.BaseMetaclasses)).Append(" | [")
                    .Append(row.File).Append("](").Append(row.File).Append(") | ").Append(row.Source).Append(" |\n");
            }

            return builder.ToString();
        }
    }
}

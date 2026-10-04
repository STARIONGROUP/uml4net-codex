// -------------------------------------------------------------------------------------------------
// <copyright file="AssociationFileGenerator.cs" company="Starion Group S.A.">
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
    using System.Text;

    using uml4net.StructuredClassifiers;

    using Uml4Net.Sage.MetamodelGen.Markdown;
    using Uml4Net.Sage.MetamodelGen.Model;

    /// <summary>
    /// Renders one <see cref="IAssociation"/> - a metamodel association *link* between metaclasses (e.g. the
    /// association behind <c>Class::ownedAttribute</c>), not a metaclass - to a markdown page under
    /// <c>metamodel/elements/</c> alongside classes/enumerations/primitive types, distinguished by its own
    /// <c>kind: "association"</c> front-matter field.
    /// </summary>
    public static class AssociationFileGenerator
    {
        /// <summary>
        /// Renders the markdown page for <paramref name="association"/>.
        /// </summary>
        public static string Render(IAssociation association)
        {
            var package = ElementNames.NamespaceOf(association);
            var memberEnds = AssociationExtractor.MemberEndsOf(association);

            var builder = new StringBuilder();
            builder.Append("---\n");
            builder.Append("name: \"").Append(association.Name).Append("\"\n");
            builder.Append("kind: \"association\"\n");
            builder.Append("package: \"").Append(package).Append("\"\n");
            builder.Append("qualifiedName: \"").Append(association.QualifiedName).Append("\"\n");
            builder.Append("isDerived: ").Append(association.IsDerived ? "true" : "false").Append('\n');
            builder.Append("---\n\n");

            builder.Append("# ").Append(association.Name).Append("\n\n");
            builder.Append("## Member ends\n\n");

            foreach (var end in memberEnds)
            {
                builder.Append("- **").Append(end.Name).Append("**");

                if (end.TypeName is not null)
                {
                    builder.Append(": ").Append(MarkdownHelpers.Link(end.TypeName, end.TypeQualifiedName));
                }

                builder.Append(" [").Append(end.Lower).Append("..").Append(end.Upper).Append(']');

                var modifiers = new List<string>();
                if (end.IsDerived)
                {
                    modifiers.Add("derived");
                }

                if (end.IsOrdered)
                {
                    modifiers.Add("ordered");
                }

                if (end.IsUnique is false)
                {
                    modifiers.Add("non-unique");
                }

                if (end.IsComposite)
                {
                    modifiers.Add("composite");
                }

                if (end.Aggregation == "shared")
                {
                    modifiers.Add("shared");
                }

                if (!end.IsNavigable)
                {
                    modifiers.Add("non-navigable");
                }

                if (modifiers.Count > 0)
                {
                    builder.Append(" *(").Append(string.Join(", ", modifiers)).Append(")*");
                }

                if (end.IsOwnedByAssociation)
                {
                    builder.Append("\n  - owned by this association, not by either endpoint classifier");
                }

                builder.Append('\n');
            }

            builder.Append("\n## Description\n\n");
            builder.Append(MarkdownHelpers.Documentation(association) ?? "_No description available._").Append('\n');

            return builder.ToString();
        }
    }
}

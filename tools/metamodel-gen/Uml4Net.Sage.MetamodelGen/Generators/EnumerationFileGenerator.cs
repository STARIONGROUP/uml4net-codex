// -------------------------------------------------------------------------------------------------
// <copyright file="EnumerationFileGenerator.cs" company="Starion Group S.A.">
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
    using System.Linq;
    using System.Text;

    using uml4net.SimpleClassifiers;

    using Uml4Net.Sage.MetamodelGen.Markdown;

    /// <summary>
    /// Renders one <see cref="IEnumeration"/> to a markdown page: front matter, its owned literals (each
    /// with its own description when the XMI's <c>ownedComment</c> provides one), and the enumeration's
    /// own description.
    /// </summary>
    public static class EnumerationFileGenerator
    {
        /// <summary>
        /// Renders the markdown page for <paramref name="enumeration"/>.
        /// </summary>
        public static string Render(IEnumeration enumeration)
        {
            var package = ElementNames.NamespaceOf(enumeration);

            var builder = new StringBuilder();
            builder.Append("---\n");
            builder.Append("name: \"").Append(enumeration.Name).Append("\"\n");
            builder.Append("kind: \"enumeration\"\n");
            builder.Append("package: \"").Append(package).Append("\"\n");
            builder.Append("qualifiedName: \"").Append(enumeration.QualifiedName).Append("\"\n");
            builder.Append("---\n\n");

            builder.Append("# ").Append(enumeration.Name).Append("\n\n");
            builder.Append("## Literals\n\n");

            // ownedLiteral is an ordered property: keep the XMI's (normative) order rather than sorting,
            // since the order of literals is part of an enumeration's definition.
            var literals = enumeration.OwnedLiteral.ToList();
            if (literals.Count == 0)
            {
                builder.Append("_None._\n");
            }
            else
            {
                foreach (var literal in literals)
                {
                    builder.Append("- `").Append(literal.Name).Append('`');

                    var literalDescription = MarkdownHelpers.Documentation(literal);
                    if (literalDescription is not null)
                    {
                        builder.Append(" - ").Append(literalDescription);
                    }

                    builder.Append('\n');
                }
            }

            builder.Append("\n## Description\n\n");
            builder.Append(MarkdownHelpers.Documentation(enumeration) ?? "_No description available._").Append('\n');

            return builder.ToString();
        }
    }
}

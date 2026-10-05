// -------------------------------------------------------------------------------------------------
// <copyright file="PrimitiveTypeFileGenerator.cs" company="Starion Group S.A.">
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
    using System.Text;

    using uml4net.SimpleClassifiers;

    using Uml4Net.Sage.MetamodelGen.Markdown;

    /// <summary>
    /// Renders one <see cref="IPrimitiveType"/> to a markdown page: front matter and its own documentation comment.
    /// </summary>
    public static class PrimitiveTypeFileGenerator
    {
        /// <summary>
        /// Renders the markdown page for <paramref name="primitiveType"/>.
        /// </summary>
        public static string Render(IPrimitiveType primitiveType)
        {
            var package = ElementNames.NamespaceOf(primitiveType);
            var documentation = MarkdownHelpers.Documentation(primitiveType);

            var builder = new StringBuilder();
            builder.Append("---\n");
            builder.Append("name: \"").Append(primitiveType.Name).Append("\"\n");
            builder.Append("kind: \"primitiveType\"\n");
            builder.Append("package: \"").Append(package).Append("\"\n");
            builder.Append("qualifiedName: \"").Append(primitiveType.QualifiedName).Append("\"\n");
            builder.Append("---\n\n");

            builder.Append("# ").Append(primitiveType.Name).Append("\n\n");
            builder.Append(string.IsNullOrWhiteSpace(documentation) ? "_No description available._" : documentation).Append('\n');

            return builder.ToString();
        }
    }
}

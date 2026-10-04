// -------------------------------------------------------------------------------------------------
// <copyright file="StereotypeFileGenerator.cs" company="Starion Group S.A.">
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

    using uml4net.Packages;

    using Uml4Net.Sage.MetamodelGen.Markdown;
    using Uml4Net.Sage.MetamodelGen.Model;

    /// <summary>
    /// Renders one <see cref="IStereotype"/> to a markdown page. Written under <c>standard-profile/pages/</c>,
    /// never <c>metamodel/elements/</c> - a stereotype is not a metaclass, even though <see cref="IStereotype"/>
    /// happens to extend <see cref="IClass"/> in the UML metamodel itself.
    /// </summary>
    public static class StereotypeFileGenerator
    {
        /// <summary>
        /// Renders the markdown page for <paramref name="stereotype"/>.
        /// </summary>
        /// <param name="stereotype">The stereotype to render.</param>
        /// <param name="graph">
        /// A <see cref="ClassGraph"/> built over the Standard Profile's own stereotypes (every <see cref="IStereotype"/>
        /// is also an <see cref="IClass"/>), used to resolve the stereotype's own generalizations/specializations
        /// (e.g. <c>Document</c> extends <c>File</c>) the same way <see cref="MetaclassFileGenerator"/> does for metaclasses.
        /// </param>
        /// <remarks>
        /// Base metaclasses are read from the stereotype's own <see cref="IExtension"/>s via
        /// <see cref="IExtension.Metaclass"/>. <see cref="IClass.Extension"/> also returns the extensions of the
        /// stereotype's specializations (its OCL includes <c>endTypes.allParents()</c>), so only extensions whose
        /// <see cref="IExtensionEnd"/> is typed by this very stereotype are kept - an inherited extension is reported
        /// on the specializing stereotype, not repeated on its general.
        /// </remarks>
        public static string Render(IStereotype stereotype, ClassGraph graph)
        {
            var qualifiedName = stereotype.QualifiedName;
            var generalizations = graph.DirectSuperClassesOf(qualifiedName);
            var specializations = graph.DirectSubclassesOf(qualifiedName);

            var extensions = ExtensionsOf(stereotype);
            var baseMetaclasses = extensions.Select(extension => extension.Metaclass).ToList();

            var taggedValues = stereotype.OwnedAttribute
                .Where(attribute => attribute.Association is not IExtension)
                .Select(attribute => attribute.Name)
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToList();

            var documentation = MarkdownHelpers.Documentation(stereotype);

            var builder = new StringBuilder();
            builder.Append("---\n");
            builder.Append("name: \"").Append(stereotype.Name).Append("\"\n");
            builder.Append("kind: \"stereotype\"\n");
            builder.Append("qualifiedName: \"").Append(stereotype.QualifiedName).Append("\"\n");
            builder.Append("profile: \"").Append(ProfileOf(stereotype)).Append("\"\n");
            builder.Append("baseMetaclasses: [").Append(string.Join(", ", baseMetaclasses.Select(name => $"\"{name}\""))).Append("]\n");
            builder.Append("---\n\n");

            builder.Append("# «").Append(stereotype.Name).Append("»\n\n");

            builder.Append("## Generalizations\n\n").Append(MarkdownHelpers.LinkList(generalizations)).Append("\n\n");
            builder.Append("## Specializations\n\n").Append(MarkdownHelpers.LinkList(specializations)).Append("\n\n");

            builder.Append("## Base metaclasses\n\n");
            builder.Append(extensions.Count == 0
                ? "_None._"
                : string.Join("\n", extensions.Select(extension => extension.IsRequired ? $"- `{extension.Metaclass}` *(required)*" : $"- `{extension.Metaclass}`"))).Append("\n\n");

            builder.Append("## Tagged values\n\n");
            builder.Append(taggedValues.Count == 0 ? "_None._" : string.Join("\n", taggedValues.Select(name => $"- `{name}`"))).Append("\n\n");

            builder.Append("## Description\n\n");
            builder.Append(string.IsNullOrWhiteSpace(documentation) ? "_No description available._" : documentation).Append('\n');

            return builder.ToString();
        }

        /// <summary>
        /// Gets the metaclasses <paramref name="stereotype"/> itself (not one of its specializations) extends, with
        /// whether each extension is required, ordered by metaclass name (ordinal).
        /// </summary>
        /// <param name="stereotype">The stereotype whose extensions are queried.</param>
        /// <returns>The stereotype's own extensions.</returns>
        public static IReadOnlyList<StereotypeExtensionInfo> ExtensionsOf(IStereotype stereotype)
        {
            return stereotype.Extension
                .Where(extension => extension.OwnedEnd.Any(end => ReferenceEquals(end.Type, stereotype)))
                .Where(extension => !string.IsNullOrEmpty(extension.Metaclass?.Name))
                .Select(extension => new StereotypeExtensionInfo(extension.Metaclass.Name, extension.IsRequired))
                .DistinctBy(extension => extension.Metaclass, System.StringComparer.Ordinal)
                .OrderBy(extension => extension.Metaclass, System.StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Gets the qualified name of the profile that owns <paramref name="stereotype"/>, or
        /// <see cref="string.Empty"/> when it isn't owned by one.
        /// </summary>
        /// <param name="stereotype">The stereotype whose profile is queried.</param>
        /// <returns>The profile's qualified name, or <see cref="string.Empty"/>.</returns>
        public static string ProfileOf(IStereotype stereotype)
        {
            return stereotype.Profile?.QualifiedName ?? string.Empty;
        }
    }
}

// -------------------------------------------------------------------------------------------------
// <copyright file="MarkdownHelpers.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.MetamodelGen.Markdown
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using uml4net.CommonStructure;
    using uml4net.Extensions;

    using Uml4Net.Sage.MetamodelGen.Model;

    /// <summary>
    /// Small rendering helpers shared by the per-element markdown generators, kept deterministic
    /// (ordinal sorting, LF line endings via callers writing with <c>"\n"</c>) per the knowledge-base
    /// determinism discipline documented in the root CLAUDE.md.
    /// </summary>
    public static class MarkdownHelpers
    {
        /// <summary>
        /// Renders a link to another element's markdown page, or the plain name if no qualified name is known.
        /// </summary>
        public static string Link(string name, string? qualifiedName)
        {
            if (string.IsNullOrEmpty(qualifiedName))
            {
                return name;
            }

            var simpleName = qualifiedName.Contains("::") ? qualifiedName[(qualifiedName.LastIndexOf("::", System.StringComparison.Ordinal) + 2)..] : qualifiedName;
            return $"[{name}]({simpleName}.md)";
        }

        /// <summary>
        /// Renders one feature's signature line, e.g. <c>+ name: [Type](Type.md) [0..*] {derived, ordered}</c>.
        /// An operation's name is followed by its parenthesized parameter list (e.g. <c>name(p: [Type](Type.md))</c>,
        /// or <c>name()</c> for a no-argument operation) so it can never be mistaken for an attribute of the
        /// same name.
        /// </summary>
        public static string FeatureSignature(FeatureInfo feature)
        {
            var builder = new StringBuilder();
            builder.Append("- **").Append(feature.Name);

            if (feature.Kind == "operation")
            {
                builder.Append('(').Append(ParameterList(feature.Parameters)).Append(')');
            }

            builder.Append("**");

            if (feature.TypeName is not null)
            {
                builder.Append(": ").Append(Link(feature.TypeName, feature.TypeQualifiedName));
            }

            builder.Append(" [").Append(feature.Lower).Append("..").Append(feature.Upper).Append(']');

            var modifiers = Modifiers(feature);
            if (modifiers.Count > 0)
            {
                builder.Append(" *(").Append(string.Join(", ", modifiers)).Append(")*");
            }

            if (feature.DefaultValue is not null)
            {
                builder.Append("\n  - default `").Append(feature.DefaultValue).Append('`');
            }

            if (feature.Opposite is not null)
            {
                builder.Append("\n  - opposite `").Append(feature.Opposite).Append('`');
            }

            foreach (var redefined in feature.Redefines.OrderBy(name => name, System.StringComparer.Ordinal))
            {
                builder.Append("\n  - redefines `").Append(redefined).Append('`');
            }

            foreach (var subsetted in feature.Subsets.OrderBy(name => name, System.StringComparer.Ordinal))
            {
                builder.Append("\n  - subsets `").Append(subsetted).Append('`');
            }

            foreach (var body in feature.Body)
            {
                for (var index = 0; index < body.Body.Count; index++)
                {
                    var language = index < body.Languages.Count ? body.Languages[index] : "OCL";
                    builder.Append("\n\n  ```").Append(language.ToLowerInvariant()).Append('\n');
                    builder.Append("  ").Append(body.Body[index]).Append('\n');
                    builder.Append("  ```");
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Renders an operation's parameter list, e.g. <c>n: [NamedElement](NamedElement.md), out ns: [Namespace](Namespace.md)</c>.
        /// The <c>in</c> direction is the default and left unmarked; <c>inout</c>/<c>out</c> are called out explicitly.
        /// </summary>
        public static string ParameterList(IReadOnlyList<ParameterInfo> parameters)
        {
            return string.Join(", ", parameters.Select(FormatParameter));
        }

        private static string FormatParameter(ParameterInfo parameter)
        {
            var direction = parameter.Direction is "in" or "" ? string.Empty : parameter.Direction + " ";
            var type = parameter.TypeName is null ? string.Empty : ": " + Link(parameter.TypeName, parameter.TypeQualifiedName);
            return $"{direction}{parameter.Name}{type}";
        }

        /// <summary>
        /// Renders the "## Inherited features" table: one row per inherited feature.
        /// </summary>
        public static string InheritedFeatureTable(IReadOnlyList<FeatureInfo> features)
        {
            if (features.Count == 0)
            {
                return "_None._";
            }

            var builder = new StringBuilder();
            builder.Append("| Feature | Type | Multiplicity | Owner | Modifiers |\n");
            builder.Append("|---|---|---|---|---|\n");

            foreach (var feature in features.OrderBy(f => f.Name, System.StringComparer.Ordinal).ThenBy(f => f.OwnerQualifiedName, System.StringComparer.Ordinal))
            {
                var modifiers = Modifiers(feature);
                var typeCell = feature.TypeName is null ? "" : Link(feature.TypeName, feature.TypeQualifiedName);
                var nameCell = feature.Kind == "operation" ? $"{feature.Name}({ParameterList(feature.Parameters)})" : feature.Name;
                builder.Append("| ").Append(nameCell)
                    .Append(" | ").Append(typeCell)
                    .Append(" | [").Append(feature.Lower).Append("..").Append(feature.Upper).Append(']')
                    .Append(" | ").Append(Link(SimpleName(feature.OwnerQualifiedName), feature.OwnerQualifiedName))
                    .Append(" | ").Append(string.Join(", ", modifiers))
                    .Append(" |\n");
            }

            return builder.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// Lists the UML property/operation modifiers that apply to <paramref name="feature"/>, in a fixed order
        /// (e.g. <c>derived, union, readOnly, ordered</c>).
        /// </summary>
        public static IReadOnlyList<string> Modifiers(FeatureInfo feature)
        {
            var modifiers = new List<string>();

            void AddIf(bool condition, string modifier)
            {
                if (condition)
                {
                    modifiers.Add(modifier);
                }
            }

            AddIf(feature.IsDerived, "derived");
            AddIf(feature.IsDerivedUnion, "union");
            AddIf(feature.IsReadOnly, "readOnly");
            AddIf(feature.IsStatic, "static");
            AddIf(feature.IsOrdered, "ordered");
            AddIf(feature.IsUnique is false, "non-unique");
            AddIf(feature.IsComposite, "composite");
            AddIf(feature.Aggregation == "shared", "shared");
            AddIf(feature.IsQuery, "query");
            AddIf(feature.IsAbstract, "abstract");

            return modifiers;
        }

        /// <summary>
        /// Renders a bullet list of links, or "_None._" when empty.
        /// </summary>
        public static string LinkList(IEnumerable<string> qualifiedNames)
        {
            var ordered = qualifiedNames.OrderBy(name => name, System.StringComparer.Ordinal).ToList();
            if (ordered.Count == 0)
            {
                return "_None._";
            }

            return string.Join("\n", ordered.Select(name => $"- {Link(SimpleName(name), name)}"));
        }

        /// <summary>
        /// Extracts the simple (unqualified) name from a "::"-separated qualified name.
        /// </summary>
        public static string SimpleName(string qualifiedName)
        {
            var index = qualifiedName.LastIndexOf("::", System.StringComparison.Ordinal);
            return index < 0 ? qualifiedName : qualifiedName[(index + 2)..];
        }

        /// <summary>
        /// Gets <paramref name="element"/>'s documentation: every non-empty <c>ownedComment</c> body, joined, with
        /// line breaks collapsed and inline HTML tags removed (uml4net.Extensions'
        /// <see cref="uml4net.Extensions.ElementExtensions.QueryRawDocumentation"/>), or <see langword="null"/>
        /// when the element has no documentation.
        /// </summary>
        public static string? Documentation(IElement element)
        {
            var documentation = element.QueryRawDocumentation();
            return string.IsNullOrWhiteSpace(documentation) ? null : documentation;
        }
    }
}

// -------------------------------------------------------------------------------------------------
// <copyright file="MetaclassFileGenerator.cs" company="Starion Group S.A.">
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

    using uml4net.StructuredClassifiers;

    using Uml4Net.Sage.MetamodelGen;
    using Uml4Net.Sage.MetamodelGen.Markdown;
    using Uml4Net.Sage.MetamodelGen.Model;

    /// <summary>
    /// Renders one metaclass (<see cref="IClass"/>) to a markdown page: front matter, generalizations,
    /// specializations, owned features, the full inherited-feature closure, and OCL constraints.
    /// </summary>
    public static class MetaclassFileGenerator
    {
        /// <summary>
        /// Renders the markdown page for <paramref name="class"/>.
        /// </summary>
        public static string Render(IClass @class, ClassGraph graph)
        {
            var qualifiedName = @class.QualifiedName;
            var package = ElementNames.NamespaceOf(@class);
            var generalizations = graph.DirectSuperClassesOf(qualifiedName);
            var specializations = graph.DirectSubclassesOf(qualifiedName);

            var ownedAttributes = @class.OwnedAttribute.Select(a => FeatureExtractor.FromProperty(a, qualifiedName)).ToList();
            var ownedOperations = @class.OwnedOperation.Select(o => FeatureExtractor.FromOperation(o, qualifiedName)).ToList();
            var inheritedFeatures = FeatureExtractor.InheritedFeaturesOf(@class);
            var constraints = ConstraintExtractor.FromNamespace(@class);

            var builder = new StringBuilder();
            builder.Append("---\n");
            builder.Append("name: \"").Append(@class.Name).Append("\"\n");
            builder.Append("kind: \"class\"\n");
            builder.Append("package: \"").Append(package).Append("\"\n");
            builder.Append("qualifiedName: \"").Append(qualifiedName).Append("\"\n");
            builder.Append("isAbstract: ").Append(@class.IsAbstract ? "true" : "false").Append('\n');
            builder.Append("visibility: \"").Append(@class.Visibility).Append("\"\n");
            builder.Append("---\n\n");

            builder.Append("# ").Append(@class.Name).Append("\n\n");

            builder.Append("## Generalizations\n\n").Append(MarkdownHelpers.LinkList(generalizations)).Append("\n\n");
            builder.Append("## Specializations\n\n").Append(MarkdownHelpers.LinkList(specializations)).Append("\n\n");

            builder.Append("## Owned features\n\n");
            AppendFeatureList(builder, ownedAttributes);
            AppendFeatureList(builder, ownedOperations);
            if (ownedAttributes.Count == 0 && ownedOperations.Count == 0)
            {
                builder.Append("_None._\n");
            }

            builder.Append("\n## Inherited features\n\n").Append(MarkdownHelpers.InheritedFeatureTable(inheritedFeatures)).Append("\n\n");

            builder.Append("## Constraints\n\n");
            AppendConstraints(builder, constraints);

            builder.Append("\n## Description\n\n");
            builder.Append(MarkdownHelpers.Documentation(@class) ?? "_No description available._").Append('\n');

            return builder.ToString();
        }

        private static void AppendFeatureList(StringBuilder builder, IReadOnlyList<FeatureInfo> features)
        {
            foreach (var feature in features.OrderBy(f => f.Name, System.StringComparer.Ordinal))
            {
                builder.Append(MarkdownHelpers.FeatureSignature(feature)).Append('\n');
            }
        }

        private static void AppendConstraints(StringBuilder builder, IReadOnlyList<ConstraintInfo> constraints)
        {
            if (constraints.Count == 0)
            {
                builder.Append("_None._\n");
                return;
            }

            foreach (var constraint in constraints)
            {
                builder.Append("### ").Append(constraint.Name ?? "(unnamed)").Append('\n');
                for (var index = 0; index < constraint.Body.Count; index++)
                {
                    var language = index < constraint.Languages.Count ? constraint.Languages[index] : "OCL";
                    builder.Append("```").Append(language.ToLowerInvariant()).Append('\n');
                    builder.Append(constraint.Body[index]).Append('\n');
                    builder.Append("```\n\n");
                }
            }
        }
    }
}

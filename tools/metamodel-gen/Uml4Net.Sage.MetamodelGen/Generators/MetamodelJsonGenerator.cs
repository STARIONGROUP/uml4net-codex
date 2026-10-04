// -------------------------------------------------------------------------------------------------
// <copyright file="MetamodelJsonGenerator.cs" company="Starion Group S.A.">
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
    using System.Text.Json;

    using Uml4Net.Sage.MetamodelGen;
    using Uml4Net.Sage.MetamodelGen.Model;

    /// <summary>
    /// Builds and serializes <c>metamodel/metamodel.json</c>: the full class/enumeration/primitive-type
    /// graph with precomputed inheritance closures.
    /// </summary>
    public static class MetamodelJsonGenerator
    {
        /// <summary>
        /// The fixed <see cref="JsonSerializerOptions"/> used for every generated JSON file, so
        /// regeneration is byte-for-byte deterministic (see CLAUDE.md, "Determinism of generated artifacts").
        /// </summary>
        public static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>
        /// Builds the document for the given catalog.
        /// </summary>
        public static MetamodelJsonDocument Build(ElementCatalog catalog, ClassGraph graph)
        {
            var classNodes = catalog.Classes
                .Where(c => !string.IsNullOrEmpty(c.QualifiedName))
                .Select(c => BuildClassNode(c, graph))
                .OrderBy(node => node.QualifiedName, System.StringComparer.Ordinal)
                .ToList();

            var enumerationNodes = catalog.Enumerations
                .Where(e => !string.IsNullOrEmpty(e.QualifiedName))
                .Select(e => new EnumerationJsonNode(
                    e.Name,
                    e.QualifiedName,
                    ElementNames.NamespaceOf(e),
                    // ownedLiteral is ordered: keep the XMI's (normative) literal order, not alphabetical.
                    e.OwnedLiteral.Select(l => l.Name).ToList()))
                .OrderBy(node => node.QualifiedName, System.StringComparer.Ordinal)
                .ToList();

            var primitiveTypeNodes = catalog.PrimitiveTypes
                .Where(p => !string.IsNullOrEmpty(p.QualifiedName))
                .Select(p => new PrimitiveTypeJsonNode(p.Name, p.QualifiedName, ElementNames.NamespaceOf(p)))
                .OrderBy(node => node.QualifiedName, System.StringComparer.Ordinal)
                .ToList();

            var associationNodes = catalog.Associations
                .Where(a => !string.IsNullOrEmpty(a.QualifiedName))
                .Select(a => new AssociationJsonNode(
                    a.Name,
                    a.QualifiedName,
                    ElementNames.NamespaceOf(a),
                    a.IsAbstract,
                    a.IsDerived,
                    AssociationExtractor.MemberEndsOf(a)))
                .OrderBy(node => node.QualifiedName, System.StringComparer.Ordinal)
                .ToList();

            return new MetamodelJsonDocument(classNodes, enumerationNodes, primitiveTypeNodes, associationNodes);
        }

        /// <summary>
        /// Serializes a document to its deterministic JSON text (LF line endings via the caller writing the file).
        /// </summary>
        public static string Serialize(MetamodelJsonDocument document)
        {
            return JsonSerializer.Serialize(document, SerializerOptions);
        }

        private static MetamodelJsonNode BuildClassNode(uml4net.StructuredClassifiers.IClass @class, ClassGraph graph)
        {
            var qualifiedName = @class.QualifiedName;
            var package = ElementNames.NamespaceOf(@class);

            var ownedAttributes = @class.OwnedAttribute
                .Select(a => FeatureExtractor.FromProperty(a, qualifiedName))
                .OrderBy(f => f.Name, System.StringComparer.Ordinal)
                .ToList();

            var ownedOperations = @class.OwnedOperation
                .Select(o => FeatureExtractor.FromOperation(o, qualifiedName))
                .OrderBy(f => f.Name, System.StringComparer.Ordinal)
                .ToList();

            var inheritedFeatures = FeatureExtractor.InheritedFeaturesOf(@class);

            return new MetamodelJsonNode(
                Name: @class.Name,
                QualifiedName: qualifiedName,
                Package: package,
                IsAbstract: @class.IsAbstract,
                DirectSuperClasses: graph.DirectSuperClassesOf(qualifiedName),
                DirectSubclasses: graph.DirectSubclassesOf(qualifiedName),
                AllAncestors: graph.AllAncestorsOf(qualifiedName),
                AllDescendants: graph.AllDescendantsOf(qualifiedName),
                OwnedAttributes: ownedAttributes,
                OwnedOperations: ownedOperations,
                InheritedAttributes: inheritedFeatures.Where(f => f.Kind == "attribute").ToList(),
                InheritedOperations: inheritedFeatures.Where(f => f.Kind == "operation").ToList(),
                Constraints: ConstraintExtractor.FromNamespace(@class));
        }
    }
}

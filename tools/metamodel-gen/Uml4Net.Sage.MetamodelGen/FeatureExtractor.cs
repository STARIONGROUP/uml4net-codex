// -------------------------------------------------------------------------------------------------
// <copyright file="FeatureExtractor.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.MetamodelGen
{
    using System.Linq;

    using uml4net.Classification;
    using uml4net.CommonStructure;
    using uml4net.StructuredClassifiers;

    using Uml4Net.Sage.MetamodelGen.Model;

    /// <summary>
    /// Converts <see cref="IProperty"/>/<see cref="IOperation"/> instances read from XMI into render-ready
    /// <see cref="FeatureInfo"/> records (the MODEL provenance tier: read directly from the metamodel XMI).
    /// </summary>
    public static class FeatureExtractor
    {
        /// <summary>
        /// Converts an owned attribute.
        /// </summary>
        public static FeatureInfo FromProperty(IProperty property, string ownerQualifiedName)
        {
            var type = property.Type as INamedElement;

            return new FeatureInfo(
                Name: property.Name,
                Kind: "attribute",
                TypeName: type?.Name,
                TypeQualifiedName: type?.QualifiedName,
                Lower: property.Lower,
                Upper: property.Upper,
                IsDerived: property.IsDerived,
                IsOrdered: property.IsOrdered,
                IsUnique: property.IsUnique,
                IsComposite: property.IsComposite,
                Redefines: property.RedefinedProperty.Where(p => !string.IsNullOrEmpty(p.QualifiedName)).Select(p => p.QualifiedName).ToList(),
                Subsets: property.SubsettedProperty.Where(p => !string.IsNullOrEmpty(p.QualifiedName)).Select(p => p.QualifiedName).ToList(),
                OwnerQualifiedName: ownerQualifiedName,
                Parameters: [],
                Body: []);
        }

        /// <summary>
        /// Converts an owned operation.
        /// </summary>
        public static FeatureInfo FromOperation(IOperation operation, string ownerQualifiedName)
        {
            var type = operation.Type as INamedElement;

            var parameters = operation.OwnedParameter
                .Where(parameter => parameter.Direction != ParameterDirectionKind.Return)
                .Select(parameter =>
                {
                    var parameterType = parameter.Type as INamedElement;

                    return new ParameterInfo(
                        Name: parameter.Name,
                        TypeName: parameterType?.Name,
                        TypeQualifiedName: parameterType?.QualifiedName,
                        Lower: parameter.Lower,
                        Upper: parameter.Upper,
                        Direction: parameter.Direction.ToString().ToLowerInvariant());
                })
                .ToList();

            return new FeatureInfo(
                Name: operation.Name,
                Kind: "operation",
                TypeName: type?.Name,
                TypeQualifiedName: type?.QualifiedName,
                Lower: operation.Lower ?? 0,
                Upper: operation.Upper,
                IsDerived: false,
                IsOrdered: operation.IsOrdered,
                IsUnique: operation.IsUnique,
                IsComposite: false,
                Redefines: operation.RedefinedOperation.Where(o => !string.IsNullOrEmpty(o.QualifiedName)).Select(o => o.QualifiedName).ToList(),
                Subsets: [],
                OwnerQualifiedName: ownerQualifiedName,
                Parameters: parameters,
                Body: ConstraintExtractor.FromOperationBody(operation));
        }
    }
}

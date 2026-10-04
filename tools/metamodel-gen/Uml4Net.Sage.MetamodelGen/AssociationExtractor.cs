// -------------------------------------------------------------------------------------------------
// <copyright file="AssociationExtractor.cs" company="Starion Group S.A.">
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
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using uml4net.Classification;
    using uml4net.CommonStructure;
    using uml4net.StructuredClassifiers;

    using Uml4Net.Sage.MetamodelGen.Model;

    /// <summary>
    /// Converts an <see cref="IAssociation"/>'s <c>memberEnd</c> properties read from XMI into render-ready
    /// <see cref="AssociationEndInfo"/> records (the MODEL provenance tier: read directly from the metamodel XMI).
    /// </summary>
    public static class AssociationExtractor
    {
        /// <summary>
        /// Converts every member end of <paramref name="association"/>, in declaration order.
        /// </summary>
        public static IReadOnlyList<AssociationEndInfo> MemberEndsOf(IAssociation association)
        {
            return association.MemberEnd
                .Select(end =>
                {
                    var type = end.Type as INamedElement;

                    return new AssociationEndInfo(
                        Name: end.Name,
                        TypeName: type?.Name,
                        TypeQualifiedName: type?.QualifiedName,
                        Lower: end.Lower,
                        Upper: end.Upper,
                        IsOrdered: end.IsOrdered,
                        IsUnique: end.IsUnique,
                        IsComposite: end.IsComposite,
                        IsOwnedByAssociation: ReferenceEquals(end.OwningAssociation, association),
                        IsDerived: end.IsDerived,
                        Aggregation: end.Aggregation.ToString().ToLowerInvariant(),
                        IsNavigable: end.OwningAssociation is null || association.NavigableOwnedEnd.Contains(end),
                        Opposite: end.Opposite?.QualifiedName);
                })
                .ToList();
        }
    }
}

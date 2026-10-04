// -------------------------------------------------------------------------------------------------
// <copyright file="FeatureInfo.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.MetamodelGen.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// A metaclass-agnostic, render-ready description of one owned or inherited attribute or operation.
    /// </summary>
    /// <param name="Name">The feature's own name.</param>
    /// <param name="Kind">Either "attribute" or "operation".</param>
    /// <param name="TypeName">The simple name of the feature's type, if any.</param>
    /// <param name="TypeQualifiedName">The fully qualified name of the feature's type, if any, for linking.</param>
    /// <param name="Lower">The lower multiplicity bound.</param>
    /// <param name="Upper">The upper multiplicity bound ("*" for unbounded).</param>
    /// <param name="IsDerived">Whether the feature is derived.</param>
    /// <param name="IsOrdered">Whether the feature is ordered.</param>
    /// <param name="IsUnique">Whether the feature is unique.</param>
    /// <param name="IsComposite">Whether the feature is a composite (owned attributes only).</param>
    /// <param name="Redefines">The qualified names of features this one redefines.</param>
    /// <param name="Subsets">The qualified names of features this one subsets.</param>
    /// <param name="OwnerQualifiedName">The qualified name of the metaclass that owns this feature.</param>
    /// <param name="Parameters">
    /// For an operation, its <c>in</c>/<c>inout</c>/<c>out</c> parameters in declaration order (never sorted -
    /// call-site order is semantically meaningful); always empty for attributes.
    /// </param>
    /// <param name="Body">
    /// For an operation with a <c>bodyCondition</c>, the OCL specification that backs it (the formula behind
    /// a derived property's <c>Query*()</c> accessor, e.g. <c>Connector::kind()</c>); empty for attributes and
    /// for operations with no <c>bodyCondition</c>.
    /// </param>
    /// <param name="IsStatic">Whether the feature is static (owned by the classifier rather than its instances).</param>
    /// <param name="IsReadOnly">Whether the attribute is read-only; always false for operations.</param>
    /// <param name="IsDerivedUnion">
    /// Whether the attribute is a derived union - its value is the union of every property that subsets it;
    /// always false for operations.
    /// </param>
    /// <param name="Aggregation">The attribute's aggregation (<c>none</c>, <c>shared</c> or <c>composite</c>); null for operations.</param>
    /// <param name="Opposite">
    /// The qualified name of the attribute's opposite association end when the association is binary, otherwise
    /// null (always null for operations).
    /// </param>
    /// <param name="DefaultValue">The attribute's default value in UML notation, if it has one; null for operations.</param>
    /// <param name="IsQuery">Whether the operation is a query (does not change the system's state); always false for attributes.</param>
    /// <param name="IsAbstract">Whether the operation is abstract; always false for attributes.</param>
    public sealed record FeatureInfo(
        string Name,
        string Kind,
        string? TypeName,
        string? TypeQualifiedName,
        int Lower,
        string Upper,
        bool IsDerived,
        bool IsOrdered,
        bool IsUnique,
        bool IsComposite,
        IReadOnlyList<string> Redefines,
        IReadOnlyList<string> Subsets,
        string OwnerQualifiedName,
        IReadOnlyList<ParameterInfo> Parameters,
        IReadOnlyList<ConstraintInfo> Body,
        bool IsStatic,
        bool IsReadOnly,
        bool IsDerivedUnion,
        string? Aggregation,
        string? Opposite,
        string? DefaultValue,
        bool IsQuery,
        bool IsAbstract);
}

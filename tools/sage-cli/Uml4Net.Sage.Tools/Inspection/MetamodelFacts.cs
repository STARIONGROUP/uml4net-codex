// -------------------------------------------------------------------------------------------------
// <copyright file="MetamodelFacts.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.Tools.Inspection
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    /// A feature a metaclass requires a value for: non-derived, with a lower bound of at least 1 and no default value.
    /// </summary>
    /// <param name="Name">The feature's name, e.g. <c>general</c>.</param>
    /// <param name="OwnerName">The simple name of the metaclass that declares it.</param>
    /// <param name="Lower">The lower bound.</param>
    /// <param name="Upper">The upper bound (<c>*</c> for unbounded).</param>
    public sealed record RequiredFeature(string Name, string OwnerName, int Lower, string Upper)
    {
        /// <summary>
        /// Gets <see cref="Upper"/> as a number, <see cref="int.MaxValue"/> when it is unbounded (<c>*</c>).
        /// </summary>
        public int UpperBound => int.TryParse(this.Upper, out var upper) ? upper : int.MaxValue;
    }

    /// <summary>
    /// What <see cref="XmiInspector"/> needs to know about the UML metamodel, read from the generated
    /// <c>metamodel.json</c>: which metaclasses exist, which are abstract, their ancestors, and the features
    /// each requires a value for.
    /// </summary>
    public sealed class MetamodelFacts
    {
        private readonly Dictionary<string, MetaclassFacts> metaclasses;

        private MetamodelFacts(Dictionary<string, MetaclassFacts> metaclasses)
        {
            this.metaclasses = metaclasses;
        }

        /// <summary>
        /// Loads the facts from the <c>metamodel.json</c> at <paramref name="metamodelJsonPath"/>.
        /// </summary>
        /// <param name="metamodelJsonPath">The generated <c>metamodel.json</c>.</param>
        /// <returns>The loaded facts.</returns>
        public static MetamodelFacts Load(string metamodelJsonPath)
        {
            using var stream = File.OpenRead(metamodelJsonPath);
            using var document = JsonDocument.Parse(stream);

            var metaclasses = new Dictionary<string, MetaclassFacts>(StringComparer.Ordinal);
            foreach (var classElement in document.RootElement.GetProperty("classes").EnumerateArray())
            {
                var name = StringOf(classElement, "name");
                var ancestors = classElement.TryGetProperty("allAncestors", out var ancestorsElement)
                    ? ancestorsElement.EnumerateArray().Select(a => SimpleName(a.GetString() ?? string.Empty)).ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);

                var requiredFeatures = Features(classElement, "ownedAttributes")
                    .Concat(Features(classElement, "inheritedAttributes"))
                    .Where(feature => !feature.GetProperty("isDerived").GetBoolean() && feature.GetProperty("lower").GetInt32() >= 1 && !HasDefaultValue(feature))
                    .Select(feature => new RequiredFeature(
                        StringOf(feature, "name"),
                        SimpleName(StringOf(feature, "ownerQualifiedName")),
                        feature.GetProperty("lower").GetInt32(),
                        StringOf(feature, "upper")))
                    .OrderBy(feature => feature.Name, StringComparer.Ordinal)
                    .ToList();

                metaclasses[name] = new MetaclassFacts(classElement.GetProperty("isAbstract").GetBoolean(), ancestors, requiredFeatures);
            }

            return new MetamodelFacts(metaclasses);
        }

        /// <summary>
        /// Gets whether <paramref name="metaclass"/> is a metaclass of the metamodel at all.
        /// </summary>
        public bool IsKnown(string metaclass) => this.metaclasses.ContainsKey(metaclass);

        /// <summary>
        /// Gets whether <paramref name="metaclass"/> is abstract.
        /// </summary>
        public bool IsAbstract(string metaclass) => this.metaclasses.TryGetValue(metaclass, out var facts) && facts.IsAbstract;

        /// <summary>
        /// Gets whether <paramref name="metaclass"/> is <paramref name="general"/> or one of its specializations.
        /// </summary>
        public bool Conforms(string metaclass, string general)
        {
            return string.Equals(metaclass, general, StringComparison.Ordinal)
                || (this.metaclasses.TryGetValue(metaclass, out var facts) && facts.Ancestors.Contains(general));
        }

        /// <summary>
        /// Gets the features an instance of <paramref name="metaclass"/> requires a value for, owned and inherited.
        /// </summary>
        public IReadOnlyList<RequiredFeature> RequiredFeaturesOf(string metaclass)
        {
            return this.metaclasses.TryGetValue(metaclass, out var facts) ? facts.RequiredFeatures : [];
        }

        private static IEnumerable<JsonElement> Features(JsonElement classElement, string propertyName)
        {
            return classElement.TryGetProperty(propertyName, out var features) ? features.EnumerateArray() : [];
        }

        /// <summary>
        /// Gets whether <paramref name="feature"/> has a default value - XMI omits a value equal to its default, so
        /// an absent value is not a violation of the feature's lower bound.
        /// </summary>
        private static bool HasDefaultValue(JsonElement feature)
        {
            return feature.TryGetProperty("defaultValue", out var defaultValue) && defaultValue.ValueKind == JsonValueKind.String;
        }

        private static string StringOf(JsonElement element, string propertyName)
        {
            return element.GetProperty(propertyName).GetString() ?? string.Empty;
        }

        private static string SimpleName(string qualifiedName)
        {
            var index = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
            return index < 0 ? qualifiedName : qualifiedName[(index + 2)..];
        }

        private sealed record MetaclassFacts(bool IsAbstract, HashSet<string> Ancestors, IReadOnlyList<RequiredFeature> RequiredFeatures);
    }
}

// -------------------------------------------------------------------------------------------------
// <copyright file="ElementNames.cs" company="Starion Group S.A.">
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
    using uml4net.CommonStructure;

    /// <summary>
    /// Naming helpers shared by the extractors and generators.
    /// </summary>
    public static class ElementNames
    {
        /// <summary>
        /// Gets the qualified name of <paramref name="element"/>'s owning namespace (e.g. the package a metaclass
        /// lives in, or the class that owns a feature), or <see cref="string.Empty"/> when it has none - or, as of
        /// uml4net.xmi 9.0.0, when that namespace's qualified name is undefined because it or one of its own
        /// namespaces is unnamed.
        /// </summary>
        /// <param name="element">The element whose namespace is queried.</param>
        /// <returns>The namespace's qualified name, or <see cref="string.Empty"/>.</returns>
        public static string NamespaceOf(INamedElement element)
        {
            return (element.Namespace as INamedElement)?.QualifiedName ?? string.Empty;
        }
    }
}

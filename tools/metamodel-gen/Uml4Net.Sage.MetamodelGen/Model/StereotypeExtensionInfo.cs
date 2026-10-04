// -------------------------------------------------------------------------------------------------
// <copyright file="StereotypeExtensionInfo.cs" company="Starion Group S.A.">
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
    /// <summary>
    /// One metaclass a stereotype extends, read from the stereotype's own <c>Extension</c>.
    /// </summary>
    /// <param name="Metaclass">The simple name of the extended metaclass.</param>
    /// <param name="IsRequired">
    /// Whether every instance of the metaclass must carry the stereotype (UML <c>Extension::isRequired</c>, i.e. the
    /// extension end's lower bound is 1).
    /// </param>
    public sealed record StereotypeExtensionInfo(string Metaclass, bool IsRequired);
}

// -------------------------------------------------------------------------------------------------
// <copyright file="StandardProfileIndexGeneratorTests.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.MetamodelGen.Tests.Generators
{
    using System.Linq;

    using Uml4Net.Sage.MetamodelGen.Generators;

    [TestFixture]
    public class StandardProfileIndexGeneratorTests
    {
        [Test]
        public void BuildRows_includes_every_stereotype()
        {
            var catalog = TestFixtures.BuildCatalog();

            var rows = StandardProfileIndexGenerator.BuildRows(catalog);

            Assert.That(rows, Has.Count.EqualTo(2));
            var sampleRow = rows.Single(r => r.QualifiedName == "FixtureProfile::Sample");
            Assert.That(sampleRow.File, Is.EqualTo("pages/Sample.md"));
        }

        [Test]
        public void BuildRows_records_each_stereotypes_profile_and_own_base_metaclasses()
        {
            var rows = StandardProfileIndexGenerator.BuildRows(TestFixtures.BuildCatalog());

            var sampleRow = rows.Single(r => r.QualifiedName == "FixtureProfile::Sample");
            Assert.That(sampleRow.Profile, Is.EqualTo("FixtureProfile"));
            Assert.That(sampleRow.BaseMetaclasses, Is.EqualTo(new[] { "Widget" }));

            var specialSampleRow = rows.Single(r => r.QualifiedName == "FixtureProfile::SpecialSample");
            Assert.That(specialSampleRow.BaseMetaclasses, Is.EqualTo(new[] { "Gadget" }));
        }

        [Test]
        public void RenderMarkdown_includes_a_row_per_stereotype()
        {
            var rows = StandardProfileIndexGenerator.BuildRows(TestFixtures.BuildCatalog());

            var markdown = StandardProfileIndexGenerator.RenderMarkdown(rows);

            Assert.That(markdown, Does.Contain("FixtureProfile::Sample"));
        }
    }
}

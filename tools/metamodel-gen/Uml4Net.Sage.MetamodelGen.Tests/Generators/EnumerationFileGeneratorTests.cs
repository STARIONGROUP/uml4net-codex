// -------------------------------------------------------------------------------------------------
// <copyright file="EnumerationFileGeneratorTests.cs" company="Starion Group S.A.">
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
    public class EnumerationFileGeneratorTests
    {
        [Test]
        public void Render_includes_front_matter_and_literals()
        {
            var catalog = TestFixtures.BuildCatalog();
            var kind = catalog.Enumerations.Single(e => e.Name == "Kind");

            var markdown = EnumerationFileGenerator.Render(kind);

            Assert.That(markdown, Does.Contain("kind: \"enumeration\""));
            Assert.That(markdown, Does.Contain("qualifiedName: \"Fixture::Kind\""));
            Assert.That(markdown, Does.Contain("- `A` - Indicates the first kind."));
            Assert.That(markdown, Does.Contain("- `B`"));
        }

        [Test]
        public void Render_keeps_literals_in_normative_xmi_order_not_alphabetical()
        {
            var catalog = TestFixtures.BuildCatalog();
            var kind = catalog.Enumerations.Single(e => e.Name == "Kind");

            var markdown = EnumerationFileGenerator.Render(kind);

            var literalsSection = markdown[markdown.IndexOf("## Literals")..markdown.IndexOf("## Description")];
            Assert.That(literalsSection.IndexOf("- `A`"), Is.LessThan(literalsSection.IndexOf("- `B`")));
            Assert.That(literalsSection.IndexOf("- `B`"), Is.LessThan(literalsSection.IndexOf("- `Aa`")));
        }

        [Test]
        public void Render_says_none_for_a_literal_with_no_ownedComment()
        {
            var catalog = TestFixtures.BuildCatalog();
            var kind = catalog.Enumerations.Single(e => e.Name == "Kind");

            var markdown = EnumerationFileGenerator.Render(kind);

            var literalsSection = markdown[markdown.IndexOf("## Literals")..markdown.IndexOf("## Description")];
            Assert.That(literalsSection, Does.Contain("- `B`\n"));
            Assert.That(literalsSection, Does.Not.Contain("- `B` -"));
        }

        [Test]
        public void Render_includes_the_enumerations_own_description()
        {
            var catalog = TestFixtures.BuildCatalog();
            var kind = catalog.Enumerations.Single(e => e.Name == "Kind");

            var markdown = EnumerationFileGenerator.Render(kind);

            Assert.That(markdown, Does.Contain("## Description\n\nKind is an Enumeration for classifying a Widget."));
        }
    }
}

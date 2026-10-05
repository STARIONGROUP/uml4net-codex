// -------------------------------------------------------------------------------------------------
// <copyright file="StereotypeFileGeneratorTests.cs" company="Starion Group S.A.">
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
    public class StereotypeFileGeneratorTests
    {
        private ElementCatalog catalog;
        private ClassGraph graph;

        [SetUp]
        public void SetUp()
        {
            this.catalog = TestFixtures.BuildCatalog();
            this.graph = ClassGraph.Build(this.catalog.Stereotypes);
        }

        [Test]
        public void Render_reads_the_base_metaclass_from_the_stereotypes_extension()
        {
            var sample = this.catalog.Stereotypes.Single(s => s.Name == "Sample");

            var markdown = StereotypeFileGenerator.Render(sample, this.graph);

            Assert.That(markdown, Does.Contain("# «Sample»"));
            Assert.That(markdown, Does.Contain("baseMetaclasses: [\"Widget\"]"));
            Assert.That(markdown, Does.Contain("- `Widget`"));
        }

        [Test]
        public void Render_does_not_report_a_specializing_stereotypes_extension_on_its_general()
        {
            // uml4net's IClass.Extension follows the OCL's endTypes.allParents() branch, so Sample's Extension
            // also contains SpecialSample's Gadget extension - it must not show up as a Sample base metaclass.
            var sample = this.catalog.Stereotypes.Single(s => s.Name == "Sample");
            var specialSample = this.catalog.Stereotypes.Single(s => s.Name == "SpecialSample");

            Assert.That(StereotypeFileGenerator.Render(sample, this.graph), Does.Not.Contain("Gadget"));
            Assert.That(StereotypeFileGenerator.Render(specialSample, this.graph), Does.Contain("baseMetaclasses: [\"Gadget\"]"));
        }

        [Test]
        public void Render_excludes_the_extension_end_attribute_from_tagged_values()
        {
            var sample = this.catalog.Stereotypes.Single(s => s.Name == "Sample");

            var markdown = StereotypeFileGenerator.Render(sample, this.graph);

            var taggedValuesSection = markdown[markdown.IndexOf("## Tagged values")..markdown.IndexOf("## Description")];
            Assert.That(taggedValuesSection, Does.Contain("- `note`"));
            Assert.That(taggedValuesSection, Does.Not.Contain("base_Widget"));
        }

        [Test]
        public void Render_lists_a_stereotypes_direct_generalizations()
        {
            var specialSample = this.catalog.Stereotypes.Single(s => s.Name == "SpecialSample");

            var markdown = StereotypeFileGenerator.Render(specialSample, this.graph);

            var generalizationsSection = markdown[markdown.IndexOf("## Generalizations")..markdown.IndexOf("## Specializations")];
            Assert.That(generalizationsSection, Does.Contain("[Sample](Sample.md)"));
        }

        [Test]
        public void Render_lists_a_stereotypes_direct_specializations()
        {
            var sample = this.catalog.Stereotypes.Single(s => s.Name == "Sample");

            var markdown = StereotypeFileGenerator.Render(sample, this.graph);

            var specializationsSection = markdown[markdown.IndexOf("## Specializations")..markdown.IndexOf("## Base metaclasses")];
            Assert.That(specializationsSection, Does.Contain("[SpecialSample](SpecialSample.md)"));
        }

        [Test]
        public void Render_says_none_for_a_stereotype_with_no_generalization()
        {
            var sample = this.catalog.Stereotypes.Single(s => s.Name == "Sample");

            var markdown = StereotypeFileGenerator.Render(sample, this.graph);

            var generalizationsSection = markdown[markdown.IndexOf("## Generalizations")..markdown.IndexOf("## Specializations")];
            Assert.That(generalizationsSection, Does.Contain("_None._"));
        }
    }
}

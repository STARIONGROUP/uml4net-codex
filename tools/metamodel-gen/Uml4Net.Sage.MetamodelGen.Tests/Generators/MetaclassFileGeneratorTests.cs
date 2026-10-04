// -------------------------------------------------------------------------------------------------
// <copyright file="MetaclassFileGeneratorTests.cs" company="Starion Group S.A.">
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
    public class MetaclassFileGeneratorTests
    {
        private ElementCatalog catalog = null!;
        private ClassGraph graph = null!;

        [SetUp]
        public void SetUp()
        {
            this.catalog = TestFixtures.BuildCatalog();
            this.graph = ClassGraph.Build(this.catalog.Classes);
        }

        [Test]
        public void Render_shows_read_only_default_value_and_opposite_on_owned_attributes()
        {
            var gadget = this.catalog.Classes.Single(c => c.Name == "Gadget");

            var markdown = MetaclassFileGenerator.Render(gadget, this.graph);

            Assert.That(markdown, Does.Contain("**count**: [Integer](Integer.md) [1..1] *(readOnly)*\n  - default `0`"));
            Assert.That(markdown, Does.Contain("**container**: [Widget](Widget.md) [0..1]\n  - opposite `Fixture::A_container_gadgets::gadgets`"));
        }

        [Test]
        public void Render_includes_front_matter_generalizations_and_owned_features()
        {
            var gadget = this.catalog.Classes.Single(c => c.Name == "Gadget");

            var markdown = MetaclassFileGenerator.Render(gadget, this.graph);

            Assert.That(markdown, Does.Contain("name: \"Gadget\""));
            Assert.That(markdown, Does.Contain("kind: \"class\""));
            Assert.That(markdown, Does.Contain("qualifiedName: \"Fixture::Gadget\""));
            Assert.That(markdown, Does.Contain("isAbstract: false"));
            Assert.That(markdown, Does.Contain("[Widget](Widget.md)"));
            Assert.That(markdown, Does.Contain("**count**: [Integer](Integer.md) [1..1]"));
        }

        [Test]
        public void Render_lists_direct_subclasses_as_specializations()
        {
            var widget = this.catalog.Classes.Single(c => c.Name == "Widget");

            var markdown = MetaclassFileGenerator.Render(widget, this.graph);

            Assert.That(markdown, Does.Contain("## Specializations"));
            Assert.That(markdown, Does.Contain("[Gadget](Gadget.md)"));
            Assert.That(markdown, Does.Not.Contain("[SuperGadget]"), "SuperGadget is a grandchild, not a direct specialization");
        }

        [Test]
        public void Render_includes_transitively_inherited_attributes()
        {
            var superGadget = this.catalog.Classes.Single(c => c.Name == "SuperGadget");

            var markdown = MetaclassFileGenerator.Render(superGadget, this.graph);

            Assert.That(markdown, Does.Contain("| label |"), "label is owned by the grandparent Widget");
            Assert.That(markdown, Does.Contain("| count |"), "count is owned by the parent Gadget");
        }

        [Test]
        public void Render_renders_owned_ocl_constraints_as_fenced_code_blocks()
        {
            var widget = this.catalog.Classes.Single(c => c.Name == "Widget");

            var markdown = MetaclassFileGenerator.Render(widget, this.graph);

            Assert.That(markdown, Does.Contain("### label_not_empty"));
            Assert.That(markdown, Does.Contain("```ocl"));
            Assert.That(markdown, Does.Contain("label->notEmpty()"));
        }

        [Test]
        public void Render_renders_an_operations_bodyCondition_ocl_as_a_fenced_code_block()
        {
            var widget = this.catalog.Classes.Single(c => c.Name == "Widget");

            var markdown = MetaclassFileGenerator.Render(widget, this.graph);

            Assert.That(markdown, Does.Contain("**describe(verbosity: [Integer](Integer.md))**"));
            Assert.That(markdown, Does.Contain("result = (label)"));
        }

        [Test]
        public void Render_says_none_for_a_class_with_no_constraints()
        {
            var gadget = this.catalog.Classes.Single(c => c.Name == "Gadget");

            var markdown = MetaclassFileGenerator.Render(gadget, this.graph);

            var constraintsSection = markdown[markdown.IndexOf("## Constraints")..];
            Assert.That(constraintsSection, Does.Contain("_None._"));
        }

        [Test]
        public void Render_includes_the_classs_own_description()
        {
            var widget = this.catalog.Classes.Single(c => c.Name == "Widget");

            var markdown = MetaclassFileGenerator.Render(widget, this.graph);

            Assert.That(markdown, Does.Contain("## Description\n\nA Widget is the abstract root of the fixture's tiny class hierarchy."));
        }

        [Test]
        public void Render_says_no_description_available_for_a_class_with_no_ownedComment()
        {
            var gadget = this.catalog.Classes.Single(c => c.Name == "Gadget");

            var markdown = MetaclassFileGenerator.Render(gadget, this.graph);

            var descriptionSection = markdown[markdown.IndexOf("## Description")..];
            Assert.That(descriptionSection, Does.Contain("_No description available._"));
        }
    }
}

// -------------------------------------------------------------------------------------------------
// <copyright file="FeatureExtractorTests.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.MetamodelGen.Tests
{
    using System.Linq;

    [TestFixture]
    public class FeatureExtractorTests
    {
        [Test]
        public void FromProperty_captures_name_type_and_multiplicity()
        {
            var catalog = TestFixtures.BuildCatalog();
            var widget = catalog.Classes.Single(c => c.Name == "Widget");
            var label = widget.OwnedAttribute.Single(a => a.Name == "label");

            var feature = FeatureExtractor.FromProperty(label, widget.QualifiedName);

            Assert.That(feature.Name, Is.EqualTo("label"));
            Assert.That(feature.Kind, Is.EqualTo("attribute"));
            Assert.That(feature.TypeName, Is.EqualTo("String"));
            Assert.That(feature.Lower, Is.EqualTo(0));
            Assert.That(feature.Upper, Is.EqualTo("1"));
            Assert.That(feature.OwnerQualifiedName, Is.EqualTo("Fixture::Widget"));
        }

        [Test]
        public void FromOperation_reads_type_and_multiplicity_from_the_return_parameter()
        {
            var catalog = TestFixtures.BuildCatalog();
            var widget = catalog.Classes.Single(c => c.Name == "Widget");
            var describe = widget.OwnedOperation.Single(o => o.Name == "describe");

            var feature = FeatureExtractor.FromOperation(describe, widget.QualifiedName);

            Assert.That(feature.Kind, Is.EqualTo("operation"));
            Assert.That(feature.TypeName, Is.EqualTo("String"));
            Assert.That(feature.Lower, Is.EqualTo(1));
            Assert.That(feature.Upper, Is.EqualTo("1"));
        }

        [Test]
        public void FromOperation_captures_in_parameters_but_excludes_the_return_parameter()
        {
            var catalog = TestFixtures.BuildCatalog();
            var widget = catalog.Classes.Single(c => c.Name == "Widget");
            var describe = widget.OwnedOperation.Single(o => o.Name == "describe");

            var feature = FeatureExtractor.FromOperation(describe, widget.QualifiedName);

            Assert.That(feature.Parameters, Has.Count.EqualTo(1));
            var parameter = feature.Parameters[0];
            Assert.That(parameter.Name, Is.EqualTo("verbosity"));
            Assert.That(parameter.TypeName, Is.EqualTo("Integer"));
            Assert.That(parameter.Direction, Is.EqualTo("in"));
            Assert.That(parameter.Lower, Is.EqualTo(1));
            Assert.That(parameter.Upper, Is.EqualTo("1"));
        }

        [Test]
        public void FromProperty_has_no_parameters()
        {
            var catalog = TestFixtures.BuildCatalog();
            var widget = catalog.Classes.Single(c => c.Name == "Widget");
            var label = widget.OwnedAttribute.Single(a => a.Name == "label");

            var feature = FeatureExtractor.FromProperty(label, widget.QualifiedName);

            Assert.That(feature.Parameters, Is.Empty);
        }

        [Test]
        public void FromOperation_captures_the_bodyCondition_ocl_specification()
        {
            var catalog = TestFixtures.BuildCatalog();
            var widget = catalog.Classes.Single(c => c.Name == "Widget");
            var describe = widget.OwnedOperation.Single(o => o.Name == "describe");

            var feature = FeatureExtractor.FromOperation(describe, widget.QualifiedName);

            Assert.That(feature.Body, Has.Count.EqualTo(1));
            Assert.That(feature.Body[0].Name, Is.EqualTo("describe_body"));
            Assert.That(feature.Body[0].Body, Is.EqualTo(new[] { "result = (label)" }));
        }

        [Test]
        public void FromProperty_has_no_body()
        {
            var catalog = TestFixtures.BuildCatalog();
            var widget = catalog.Classes.Single(c => c.Name == "Widget");
            var label = widget.OwnedAttribute.Single(a => a.Name == "label");

            var feature = FeatureExtractor.FromProperty(label, widget.QualifiedName);

            Assert.That(feature.Body, Is.Empty);
        }

        [Test]
        public void FromProperty_captures_read_only_default_value_and_aggregation()
        {
            var catalog = TestFixtures.BuildCatalog();
            var gadget = catalog.Classes.Single(c => c.Name == "Gadget");
            var count = gadget.OwnedAttribute.Single(a => a.Name == "count");

            var feature = FeatureExtractor.FromProperty(count, gadget.QualifiedName);

            Assert.That(feature.IsReadOnly, Is.True);
            Assert.That(feature.DefaultValue, Is.EqualTo("0"));
            Assert.That(feature.Aggregation, Is.EqualTo("none"));
            Assert.That(feature.Opposite, Is.Null, "count is not an association end");
        }

        [Test]
        public void FromProperty_captures_the_opposite_end_of_a_binary_association()
        {
            var catalog = TestFixtures.BuildCatalog();
            var gadget = catalog.Classes.Single(c => c.Name == "Gadget");
            var container = gadget.OwnedAttribute.Single(a => a.Name == "container");

            var feature = FeatureExtractor.FromProperty(container, gadget.QualifiedName);

            Assert.That(feature.Opposite, Is.EqualTo("Fixture::A_container_gadgets::gadgets"));
            Assert.That(feature.DefaultValue, Is.Null);
        }

        [Test]
        public void FromOperation_captures_is_query_and_leaves_attribute_only_fields_empty()
        {
            var catalog = TestFixtures.BuildCatalog();
            var widget = catalog.Classes.Single(c => c.Name == "Widget");
            var describe = widget.OwnedOperation.Single(o => o.Name == "describe");

            var feature = FeatureExtractor.FromOperation(describe, widget.QualifiedName);

            Assert.That(feature.IsQuery, Is.True);
            Assert.That(feature.IsAbstract, Is.False);
            Assert.That(feature.Aggregation, Is.Null);
            Assert.That(feature.Opposite, Is.Null);
        }
    }
}

// -------------------------------------------------------------------------------------------------
// <copyright file="XmiInspectorTests.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.Tools.Tests.Inspection
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Uml4Net.Sage.Tools.Inspection;

    [TestFixture]
    public class XmiInspectorTests
    {
        private static string FixturesDirectory => Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "Fixtures");

        private static string MetamodelJsonPath => Path.Combine(FixturesDirectory, "metamodel-uml-subset.json");

        private static InspectionReport Inspect(string modelFileName, IReadOnlyDictionary<string, string>? pathMaps = null)
        {
            return XmiInspector.Inspect(Path.Combine(FixturesDirectory, modelFileName), MetamodelJsonPath, "2.5.1", pathMaps);
        }

        [Test]
        public void Inspect_of_a_valid_model_finds_nothing()
        {
            var report = Inspect("model-valid.xmi");

            Assert.That(report.Findings, Is.Empty);
        }

        [Test]
        public void Inspect_flags_an_abstract_xmi_type_with_its_id_and_line_and_reports_the_aborted_read_instead_of_throwing()
        {
            var report = Inspect("model-abstract-type.xmi");

            var abstractFinding = report.Findings.Single(f => f.Category == "abstract-instantiation");
            Assert.That(abstractFinding.Severity, Is.EqualTo("error"));
            Assert.That(abstractFinding.ElementXmiId, Is.EqualTo("Vehicle"));
            Assert.That(abstractFinding.Line, Is.EqualTo(4));
            Assert.That(abstractFinding.Message, Does.Contain("'Classifier'"));

            Assert.That(report.Findings, Has.Some.Matches<InspectionFinding>(f => f is { Category: "read-aborted" }));
        }

        [Test]
        public void Inspect_flags_an_xmi_type_that_is_not_a_uml_metaclass()
        {
            var report = Inspect("model-abstract-type.xmi");

            var finding = report.Findings.Single(f => f.Category == "unknown-metaclass");
            Assert.That(finding.ElementXmiId, Is.EqualTo("Widget"));
            Assert.That(finding.Line, Is.EqualTo(5));
        }

        [Test]
        public void Inspect_reports_an_unresolved_idref_with_the_referencing_element_property_and_line()
        {
            var report = Inspect("model-broken.xmi");

            var finding = report.Findings.Single(f => f.Category == "unresolved-reference" && f.ElementXmiId == "Car-engine");
            Assert.That(finding.Line, Is.EqualTo(6));
            Assert.That(finding.Message, Does.Contain("type references 'Engine'"));
        }

        [Test]
        public void Inspect_of_an_unresolved_external_reference_reports_it_as_a_structured_finding()
        {
            var report = Inspect("model-unresolved-reference.xmi");

            var finding = report.Findings.Single(f => f.Category == "unresolved-reference");
            Assert.That(finding.ElementXmiId, Is.EqualTo("Car-engine"));
            Assert.That(finding.Message, Does.Contain("Missing.xmi#Engine"));
            Assert.That(report.Findings, Has.None.Matches<InspectionFinding>(f => f is { Category: "reader-diagnostic" }), "the reader's own log lines for it are not repeated");
        }

        [Test]
        public void Inspect_hints_at_pathmap_when_a_pathmap_reference_is_unmapped()
        {
            var report = Inspect("model-broken.xmi");

            var finding = report.Findings.Single(f => f.Category == "unresolved-reference" && f.ElementXmiId == "Car-wheel");
            Assert.That(finding.Message, Does.Contain("no --pathmap was given"));
        }

        [Test]
        public void Inspect_resolves_a_pathmap_prefix_mapped_to_a_directory()
        {
            var pathMaps = new Dictionary<string, string> { ["pathmap://SAMPLE_LIBRARY"] = Path.Combine(FixturesDirectory, "library") };

            var report = Inspect("model-broken.xmi", pathMaps);

            Assert.That(report.Findings, Has.None.Matches<InspectionFinding>(f => f is { ElementXmiId: "Car-wheel" }));
        }

        [Test]
        public void Inspect_flags_a_required_feature_with_no_value()
        {
            var report = Inspect("model-broken.xmi");

            var finding = report.Findings.Single(f => f.Category == "multiplicity-violation");
            Assert.That(finding.ElementXmiId, Is.EqualTo("Car-gen"));
            Assert.That(finding.Line, Is.EqualTo(5));
            Assert.That(finding.Message, Does.Contain("Generalization::general [1..1] has 0 value(s)"));
        }

        [Test]
        public void Inspect_flags_a_duplicate_xmi_id_at_its_second_declaration()
        {
            var report = Inspect("model-broken.xmi");

            var finding = report.Findings.Single(f => f.Category == "duplicate-xmi-id");
            Assert.That(finding.ElementXmiId, Is.EqualTo("Truck"));
            Assert.That(finding.Line, Is.EqualTo(12));
            Assert.That(finding.Message, Does.Contain("lines 11, 12"));
        }

        [Test]
        public void Inspect_orders_findings_by_line()
        {
            var report = Inspect("model-broken.xmi");

            var lines = report.Findings.Where(f => f.Line is not null).Select(f => f.Line!.Value).ToList();
            Assert.That(lines, Is.Ordered);
        }

        [Test]
        public void Inspect_flags_a_stereotype_applied_to_a_metaclass_it_does_not_extend()
        {
            var report = Inspect("model-stereotyped.xmi");

            var finding = report.Findings.Single(f => f.Category == "stereotype-misapplied");
            Assert.That(finding.ElementXmiId, Is.EqualTo("Car-plate-Entity"));
            Assert.That(finding.Message, Does.Contain("«Entity» extends Class, but is applied to Property"));
            Assert.That(report.Findings, Has.None.Matches<InspectionFinding>(f => f is { ElementXmiId: "Car-Entity" }), "applying it to a Class is fine");
        }

        [Test]
        public void Inspect_flags_a_stereotype_application_whose_target_does_not_exist()
        {
            var report = Inspect("model-stereotyped.xmi");

            var finding = report.Findings.Single(f => f.Category == "stereotype-target-unresolved");
            Assert.That(finding.ElementXmiId, Is.EqualTo("Ghost-Entity"));
            Assert.That(finding.Line, Is.EqualTo(13));
        }

        [Test]
        public void Inspect_does_not_report_problems_in_referenced_documents()
        {
            // SampleProfile.xmi references OMG's UML.xmi (bundled with uml4net), whose own duplicate xmi:ids the
            // reader logs - they are not the inspected model's problem.
            var report = Inspect("model-stereotyped.xmi");

            Assert.That(report.Findings, Has.None.Matches<InspectionFinding>(f => f?.Message.Contains("not unique") == true));
            Assert.That(report.Findings.Select(f => f.Category), Is.EquivalentTo(new[] { "stereotype-misapplied", "stereotype-target-unresolved" }));
        }

        [Test]
        public void Inspect_records_the_model_path_and_uml_version_on_the_report()
        {
            var modelPath = Path.Combine(FixturesDirectory, "model-valid.xmi");

            var report = XmiInspector.Inspect(modelPath, MetamodelJsonPath, "2.5.1");

            Assert.That(report.ModelPath, Is.EqualTo(modelPath));
            Assert.That(report.UmlVersion, Is.EqualTo("2.5.1"));
        }
    }
}

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
    using System.IO;
    using System.Linq;

    using Uml4Net.Sage.Tools.Inspection;

    [TestFixture]
    public class XmiInspectorTests
    {
        private static string FixturesDirectory => Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "Fixtures");

        [Test]
        public void Inspect_of_a_valid_model_against_a_metamodel_with_no_abstract_classes_finds_nothing()
        {
            var report = XmiInspector.Inspect(
                Path.Combine(FixturesDirectory, "model-valid.xmi"),
                Path.Combine(FixturesDirectory, "metamodel-no-abstract-classes.json"),
                "2.5.1");

            Assert.That(report.Findings, Is.Empty);
        }

        [Test]
        public void Inspect_flags_an_element_whose_metaclass_is_abstract_in_the_metamodel()
        {
            var report = XmiInspector.Inspect(
                Path.Combine(FixturesDirectory, "model-valid.xmi"),
                Path.Combine(FixturesDirectory, "metamodel-with-abstract-class.json"),
                "2.5.1");

            var finding = report.Findings.Single(f => f.Category == "abstract-instantiation");
            Assert.That(finding.Severity, Is.EqualTo("error"));
            Assert.That(finding.Message, Does.Contain("Car"));
            Assert.That(finding.Message, Does.Contain("'Class'"));
        }

        [Test]
        public void Inspect_surfaces_an_unresolved_external_reference_as_a_reader_diagnostic_finding()
        {
            var report = XmiInspector.Inspect(
                Path.Combine(FixturesDirectory, "model-unresolved-reference.xmi"),
                Path.Combine(FixturesDirectory, "metamodel-no-abstract-classes.json"),
                "2.5.1");

            Assert.That(report.Findings, Has.Some.Matches<InspectionFinding>(f => f is { Category: "reader-diagnostic" }));
        }

        [Test]
        public void Inspect_records_the_model_path_and_uml_version_on_the_report()
        {
            var modelPath = Path.Combine(FixturesDirectory, "model-valid.xmi");

            var report = XmiInspector.Inspect(modelPath, Path.Combine(FixturesDirectory, "metamodel-no-abstract-classes.json"), "2.5.1");

            Assert.That(report.ModelPath, Is.EqualTo(modelPath));
            Assert.That(report.UmlVersion, Is.EqualTo("2.5.1"));
        }
    }
}

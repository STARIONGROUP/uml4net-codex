// -------------------------------------------------------------------------------------------------
// <copyright file="InspectCommandTests.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.Tools.Tests.Commands
{
    using System.IO;

    using Uml4Net.Sage.Tools.Commands;

    [TestFixture]
    public class InspectCommandTests
    {
        private static string FixturesDirectory => Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "Fixtures");

        private string repositoryRoot = null!;

        [SetUp]
        public void SetUp()
        {
            this.repositoryRoot = Path.Combine(Path.GetTempPath(), "uml4net-sage-tests", Path.GetRandomFileName());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(this.repositoryRoot))
            {
                Directory.Delete(this.repositoryRoot, recursive: true);
            }
        }

        [Test]
        public void Invoke_returns_1_when_the_version_has_not_been_generated_yet()
        {
            var exitCode = InspectCommand.Build().Parse(
            [
                Path.Combine(FixturesDirectory, "model-valid.xmi"),
                "--repository-root", this.repositoryRoot,
                "--version", "2.5.1",
            ]).Invoke();

            Assert.That(exitCode, Is.EqualTo(1));
        }

        [Test]
        public void Invoke_returns_0_for_a_valid_model()
        {
            var metamodelDirectory = Path.Combine(this.repositoryRoot, "knowledge", "2.5.1", "metamodel");
            Directory.CreateDirectory(metamodelDirectory);
            File.Copy(Path.Combine(FixturesDirectory, "metamodel-uml-subset.json"), Path.Combine(metamodelDirectory, "metamodel.json"));

            var exitCode = InspectCommand.Build().Parse(
            [
                Path.Combine(FixturesDirectory, "model-valid.xmi"),
                "--repository-root", this.repositoryRoot,
                "--version", "2.5.1",
            ]).Invoke();

            Assert.That(exitCode, Is.EqualTo(0));
        }

        [Test]
        public void Invoke_accepts_repeated_pathmap_options()
        {
            var metamodelDirectory = Path.Combine(this.repositoryRoot, "knowledge", "2.5.1", "metamodel");
            Directory.CreateDirectory(metamodelDirectory);
            File.Copy(Path.Combine(FixturesDirectory, "metamodel-uml-subset.json"), Path.Combine(metamodelDirectory, "metamodel.json"));

            var exitCode = InspectCommand.Build().Parse(
            [
                Path.Combine(FixturesDirectory, "model-valid.xmi"),
                "--pathmap", $"pathmap://SAMPLE_LIBRARY={Path.Combine(FixturesDirectory, "library")}",
                "--pathmap", "pathmap://OTHER/Other.xmi=Other.xmi",
                "--repository-root", this.repositoryRoot,
                "--version", "2.5.1",
            ]).Invoke();

            Assert.That(exitCode, Is.EqualTo(0));
        }

        [Test]
        public void Invoke_returns_1_for_a_pathmap_without_a_path()
        {
            var metamodelDirectory = Path.Combine(this.repositoryRoot, "knowledge", "2.5.1", "metamodel");
            Directory.CreateDirectory(metamodelDirectory);
            File.Copy(Path.Combine(FixturesDirectory, "metamodel-uml-subset.json"), Path.Combine(metamodelDirectory, "metamodel.json"));

            var exitCode = InspectCommand.Build().Parse(
            [
                Path.Combine(FixturesDirectory, "model-valid.xmi"),
                "--pathmap", "pathmap://SAMPLE_LIBRARY",
                "--repository-root", this.repositoryRoot,
                "--version", "2.5.1",
            ]).Invoke();

            Assert.That(exitCode, Is.EqualTo(1));
        }

        [Test]
        public void Invoke_returns_1_when_the_model_file_does_not_exist()
        {
            var metamodelDirectory = Path.Combine(this.repositoryRoot, "knowledge", "2.5.1", "metamodel");
            Directory.CreateDirectory(metamodelDirectory);
            File.Copy(Path.Combine(FixturesDirectory, "metamodel-uml-subset.json"), Path.Combine(metamodelDirectory, "metamodel.json"));

            var exitCode = InspectCommand.Build().Parse(
            [
                Path.Combine(this.repositoryRoot, "no-such-model.xmi"),
                "--repository-root", this.repositoryRoot,
                "--version", "2.5.1",
            ]).Invoke();

            Assert.That(exitCode, Is.EqualTo(1));
        }
    }
}

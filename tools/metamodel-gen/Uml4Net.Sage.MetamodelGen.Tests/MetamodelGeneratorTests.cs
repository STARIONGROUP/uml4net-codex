// -------------------------------------------------------------------------------------------------
// <copyright file="MetamodelGeneratorTests.cs" company="Starion Group S.A.">
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
    using System.IO;
    using System.Linq;

    [TestFixture]
    public class MetamodelGeneratorTests
    {
        private string outputRoot = null!;

        [SetUp]
        public void SetUp()
        {
            this.outputRoot = Path.Combine(Path.GetTempPath(), "uml4net-sage-tests", Path.GetRandomFileName());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(this.outputRoot))
            {
                Directory.Delete(this.outputRoot, recursive: true);
            }
        }

        [Test]
        public void Generate_writes_the_expected_metamodel_and_standard_profile_tree()
        {
            MetamodelGenerator.Generate(TestFixtures.XmiDirectory, this.outputRoot, "Fixture.xmi", "FixtureProfile.xmi");

            Assert.That(File.Exists(Path.Combine(this.outputRoot, "metamodel", "index.json")));
            Assert.That(File.Exists(Path.Combine(this.outputRoot, "metamodel", "index.md")));
            Assert.That(File.Exists(Path.Combine(this.outputRoot, "metamodel", "metamodel.json")));
            Assert.That(File.Exists(Path.Combine(this.outputRoot, "metamodel", "elements", "Widget.md")));
            Assert.That(File.Exists(Path.Combine(this.outputRoot, "metamodel", "elements", "Kind.md")));
            Assert.That(File.Exists(Path.Combine(this.outputRoot, "metamodel", "elements", "String.md")));
            Assert.That(File.Exists(Path.Combine(this.outputRoot, "metamodel", "elements", "A_container_gadgets.md")));
            Assert.That(File.Exists(Path.Combine(this.outputRoot, "standard-profile", "index.json")));
            Assert.That(File.Exists(Path.Combine(this.outputRoot, "standard-profile", "pages", "Sample.md")));
        }

        [Test]
        public void Generate_never_writes_a_stereotype_under_metamodel_elements()
        {
            MetamodelGenerator.Generate(TestFixtures.XmiDirectory, this.outputRoot, "Fixture.xmi", "FixtureProfile.xmi");

            Assert.That(File.Exists(Path.Combine(this.outputRoot, "metamodel", "elements", "Sample.md")), Is.False);
        }

        [Test]
        public void Generate_writes_utf8_without_a_byte_order_mark()
        {
            MetamodelGenerator.Generate(TestFixtures.XmiDirectory, this.outputRoot, "Fixture.xmi", "FixtureProfile.xmi");

            foreach (var path in Directory.GetFiles(this.outputRoot, "*", SearchOption.AllDirectories))
            {
                var bytes = File.ReadAllBytes(path);
                Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), $"{path} starts with a UTF-8 byte-order mark");
            }
        }

        [Test]
        public void Generate_is_byte_for_byte_deterministic_across_two_independent_runs()
        {
            var firstRun = Path.Combine(this.outputRoot, "run1");
            var secondRun = Path.Combine(this.outputRoot, "run2");

            MetamodelGenerator.Generate(TestFixtures.XmiDirectory, firstRun, "Fixture.xmi", "FixtureProfile.xmi");
            MetamodelGenerator.Generate(TestFixtures.XmiDirectory, secondRun, "Fixture.xmi", "FixtureProfile.xmi");

            var firstFiles = Directory.GetFiles(firstRun, "*", SearchOption.AllDirectories).Select(p => p[firstRun.Length..]).OrderBy(p => p).ToList();
            var secondFiles = Directory.GetFiles(secondRun, "*", SearchOption.AllDirectories).Select(p => p[secondRun.Length..]).OrderBy(p => p).ToList();

            Assert.That(firstFiles, Is.EqualTo(secondFiles));

            foreach (var relativePath in firstFiles)
            {
                var firstBytes = File.ReadAllBytes(firstRun + relativePath);
                var secondBytes = File.ReadAllBytes(secondRun + relativePath);
                Assert.That(firstBytes, Is.EqualTo(secondBytes), $"{relativePath} differs between runs");
            }
        }
    }
}

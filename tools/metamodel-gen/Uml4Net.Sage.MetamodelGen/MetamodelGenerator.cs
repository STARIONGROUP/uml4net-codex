// -------------------------------------------------------------------------------------------------
// <copyright file="MetamodelGenerator.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.MetamodelGen
{
    using System.IO;

    using Uml4Net.Sage.MetamodelGen.Generators;

    /// <summary>
    /// Orchestrates the whole metamodel/standard-profile generation pipeline: read the fetched XMI files,
    /// catalog their elements, and write the <c>metamodel/</c> and <c>standard-profile/</c> knowledge-base
    /// trees. This is what the <c>uml4net-sage generate</c> CLI verb's <c>metamodel</c> and
    /// <c>standard-profile</c> steps ultimately call.
    /// </summary>
    public static class MetamodelGenerator
    {
        /// <summary>
        /// Reads <c>UML.xmi</c> and <c>StandardProfile.xmi</c> from <paramref name="xmiSourceDirectory"/> and
        /// writes <c>metamodel/</c> and <c>standard-profile/</c> under <paramref name="knowledgeOutputDirectory"/>.
        /// </summary>
        /// <param name="xmiSourceDirectory">
        /// The directory containing the fetched UML.xmi, PrimitiveTypes.xmi and StandardProfile.xmi files
        /// (e.g. <c>sources/2.5.1/xmi/</c>).
        /// </param>
        /// <param name="knowledgeOutputDirectory">
        /// The version's knowledge-base root (e.g. <c>knowledge/2.5.1/</c>); <c>metamodel/</c> and
        /// <c>standard-profile/</c> subdirectories are created under it.
        /// </param>
        /// <param name="umlFileName">
        /// The file name (within <paramref name="xmiSourceDirectory"/>) of the abstract syntax metamodel XMI.
        /// Overridable so tests can exercise this pipeline against hand-authored fixture files without
        /// naming them "UML.xmi" (see the root CLAUDE.md's licensing discussion for why no real OMG XMI
        /// is committed as a test fixture).
        /// </param>
        /// <param name="standardProfileFileName">
        /// The file name (within <paramref name="xmiSourceDirectory"/>) of the Standard Profile XMI.
        /// </param>
        public static void Generate(string xmiSourceDirectory, string knowledgeOutputDirectory, string umlFileName = "UML.xmi", string standardProfileFileName = "StandardProfile.xmi")
        {
            var umlModel = XmiModelReader.Read(Path.Combine(xmiSourceDirectory, umlFileName), xmiSourceDirectory);
            var standardProfileModel = XmiModelReader.Read(Path.Combine(xmiSourceDirectory, standardProfileFileName), xmiSourceDirectory);

            var catalog = ElementCatalog.Build(umlModel, standardProfileModel);
            var graph = ClassGraph.Build(catalog.Classes);

            GenerateMetamodel(catalog, graph, Path.Combine(knowledgeOutputDirectory, "metamodel"));
            GenerateStandardProfile(catalog, Path.Combine(knowledgeOutputDirectory, "standard-profile"));
        }

        private static void GenerateMetamodel(ElementCatalog catalog, ClassGraph graph, string metamodelDirectory)
        {
            var elementsDirectory = Path.Combine(metamodelDirectory, "elements");
            Directory.CreateDirectory(elementsDirectory);

            foreach (var @class in catalog.Classes)
            {
                if (string.IsNullOrEmpty(@class.QualifiedName))
                {
                    continue;
                }

                WriteText(Path.Combine(elementsDirectory, $"{@class.Name}.md"), MetaclassFileGenerator.Render(@class, graph));
            }

            foreach (var enumeration in catalog.Enumerations)
            {
                if (string.IsNullOrEmpty(enumeration.QualifiedName))
                {
                    continue;
                }

                WriteText(Path.Combine(elementsDirectory, $"{enumeration.Name}.md"), EnumerationFileGenerator.Render(enumeration));
            }

            foreach (var primitiveType in catalog.PrimitiveTypes)
            {
                if (string.IsNullOrEmpty(primitiveType.QualifiedName))
                {
                    continue;
                }

                WriteText(Path.Combine(elementsDirectory, $"{primitiveType.Name}.md"), PrimitiveTypeFileGenerator.Render(primitiveType));
            }

            foreach (var association in catalog.Associations)
            {
                if (string.IsNullOrEmpty(association.QualifiedName))
                {
                    continue;
                }

                WriteText(Path.Combine(elementsDirectory, $"{association.Name}.md"), AssociationFileGenerator.Render(association));
            }

            var indexRows = MetamodelIndexGenerator.BuildRows(catalog);
            WriteText(Path.Combine(metamodelDirectory, "index.json"), MetamodelIndexGenerator.SerializeJson(indexRows));
            WriteText(Path.Combine(metamodelDirectory, "index.md"), MetamodelIndexGenerator.RenderMarkdown(indexRows));

            var jsonDocument = MetamodelJsonGenerator.Build(catalog, graph);
            WriteText(Path.Combine(metamodelDirectory, "metamodel.json"), MetamodelJsonGenerator.Serialize(jsonDocument));
        }

        private static void GenerateStandardProfile(ElementCatalog catalog, string standardProfileDirectory)
        {
            var pagesDirectory = Path.Combine(standardProfileDirectory, "pages");
            Directory.CreateDirectory(pagesDirectory);

            // Every IStereotype is also an IClass, so ClassGraph - already used for the metamodel's own
            // generalization/specialization closures - resolves a stereotype's Generalizations the same way.
            var stereotypeGraph = ClassGraph.Build(catalog.Stereotypes);

            foreach (var stereotype in catalog.Stereotypes)
            {
                if (string.IsNullOrEmpty(stereotype.QualifiedName))
                {
                    continue;
                }

                WriteText(Path.Combine(pagesDirectory, $"{stereotype.Name}.md"), StereotypeFileGenerator.Render(stereotype, stereotypeGraph));
            }

            var indexRows = StandardProfileIndexGenerator.BuildRows(catalog);
            WriteText(Path.Combine(standardProfileDirectory, "index.json"), StandardProfileIndexGenerator.SerializeJson(indexRows));
            WriteText(Path.Combine(standardProfileDirectory, "index.md"), StandardProfileIndexGenerator.RenderMarkdown(indexRows));
        }

        /// <summary>
        /// UTF-8 without a byte-order mark: <see cref="System.Text.Encoding.UTF8"/> writes one, which ends up in front
        /// of the markdown front matter's opening <c>---</c> and of every JSON document.
        /// </summary>
        private static readonly System.Text.UTF8Encoding Utf8WithoutByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

        private static void WriteText(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var writer = new StreamWriter(path, append: false, Utf8WithoutByteOrderMark);
            writer.NewLine = "\n";
            writer.Write(content.Replace("\r\n", "\n"));
            if (!content.EndsWith('\n'))
            {
                writer.Write('\n');
            }
        }
    }
}

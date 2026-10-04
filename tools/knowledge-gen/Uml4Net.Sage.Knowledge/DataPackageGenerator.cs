// -------------------------------------------------------------------------------------------------
// <copyright file="DataPackageGenerator.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.Knowledge
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Builds <c>knowledge/&lt;version&gt;/datapackage.json</c>: an OKF Frictionless Data Package manifest
    /// scoped to the three genuinely tabular resources (metamodel index, standard-profile index, spec clause
    /// index). Per the project's scope decision, markdown collections and the overall knowledge-base layout
    /// are <em>not</em> described here - they stay hypha's plain pattern (fixed paths, hand-documented
    /// <c>*.schema.json</c> files) since nothing in this plugin actually validates/loads a Frictionless
    /// package, and forcing non-tabular content into the Data Package resource model isn't what it's for.
    /// </summary>
    public static class DataPackageGenerator
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        /// <summary>
        /// Builds the manifest for <paramref name="version"/>, inventorying what the metamodel and spec
        /// generators actually wrote under <paramref name="knowledgeVersionDirectory"/> - the
        /// <c>spec-clause-index</c> resource is included only when <c>spec/index.json</c> exists.
        /// </summary>
        public static string Generate(string knowledgeVersionDirectory, string version, string omgDocumentId)
        {
            var hasSpecIndex = File.Exists(Path.Combine(knowledgeVersionDirectory, "spec", "index.json"));

            var document = new JsonObject
            {
                ["profile"] = "data-package",
                ["name"] = $"uml4net-sage-knowledge-{version.Replace('.', '-')}",
                ["title"] = $"uml4net Sage knowledge base indexes - OMG UML {version}",
                ["version"] = version,
                ["licenses"] = new JsonArray(
                    new JsonObject
                    {
                        ["name"] = "Apache-2.0",
                        ["path"] = "https://www.apache.org/licenses/LICENSE-2.0",
                        ["title"] = "Applies to these derivative structural indexes.",
                    }),
                ["sources"] = new JsonArray(
                    new JsonObject
                    {
                        ["title"] = $"OMG Unified Modeling Language (UML), Version {version}",
                        ["path"] = $"https://www.omg.org/spec/UML/{version}/PDF",
                        ["note"] = $"{omgDocumentId}. Not redistributed; see NOTICE.",
                    },
                    new JsonObject
                    {
                        ["title"] = "UML Abstract Syntax Metamodel XMI",
                        ["path"] = "https://www.omg.org/spec/UML/20161101/UML.xmi",
                        ["note"] = "ptc/18-01-01. Not redistributed; fetched at runtime.",
                    }),
                ["resources"] = BuildResources(hasSpecIndex),
            };

            return document.ToJsonString(SerializerOptions);
        }

        private static JsonArray BuildResources(bool hasSpecIndex)
        {
            var resources = new JsonArray
            {
                TabularResource(
                    "metamodel-index",
                    "metamodel/index.json",
                    PrimaryKeySchema(
                        ("name", "string", true),
                        ("kind", "string", false),
                        ("package", "string", false),
                        ("qualifiedName", "string", false),
                        ("isAbstract", "boolean", false),
                        ("file", "string", false)),
                    "qualifiedName"),
                TabularResource(
                    "standard-profile-index",
                    "standard-profile/index.json",
                    PrimaryKeySchema(
                        ("qualifiedName", "string", true),
                        ("kind", "string", false),
                        ("file", "string", false),
                        ("source", "string", false),
                        ("profile", "string", false),
                        ("baseMetaclasses", "array", false)),
                    "qualifiedName"),
            };

            if (hasSpecIndex)
            {
                resources.Add(TabularResource(
                    "spec-clause-index",
                    "spec/index.json",
                    PrimaryKeySchema(
                        ("clause", "string", true),
                        ("title", "string", false),
                        ("pages", "string", false),
                        ("normative", "boolean", false),
                        ("file", "string", false)),
                    "clause"));
            }

            return resources;
        }

        private static JsonObject TabularResource(string name, string path, JsonObject schema, string primaryKey)
        {
            schema["primaryKey"] = primaryKey;

            return new JsonObject
            {
                ["name"] = name,
                ["path"] = path,
                ["profile"] = "tabular-data-resource",
                ["schema"] = schema,
            };
        }

        private static JsonObject PrimaryKeySchema(params (string Name, string Type, bool Required)[] fields)
        {
            var fieldsArray = new JsonArray();
            foreach (var (name, type, required) in fields)
            {
                var field = new JsonObject { ["name"] = name, ["type"] = type };
                if (required)
                {
                    field["constraints"] = new JsonObject { ["required"] = true };
                }

                fieldsArray.Add(field);
            }

            return new JsonObject { ["fields"] = fieldsArray };
        }
    }
}

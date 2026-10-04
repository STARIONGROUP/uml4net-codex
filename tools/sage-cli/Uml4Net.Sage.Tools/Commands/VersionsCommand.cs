// -------------------------------------------------------------------------------------------------
// <copyright file="VersionsCommand.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.Tools.Commands
{
    using System;
    using System.CommandLine;
    using System.Linq;

    using Spectre.Console;

    using Uml4Net.Sage.Knowledge;

    /// <summary>
    /// <c>uml4net-sage versions</c>: lists every known UML version and its local fetch/generate state.
    /// Purely local - there is no upstream to discover against (see <see cref="KnownUmlVersions"/>).
    /// </summary>
    public static class VersionsCommand
    {
        /// <summary>
        /// Builds the command.
        /// </summary>
        public static Command Build()
        {
            var command = new Command("versions", "List every known UML version and its local fetch/generate state.");
            command.Add(GlobalOptions.RepositoryRoot);
            command.Add(GlobalOptions.Json);

            command.SetAction(parseResult =>
            {
                var repositoryRoot = RepositoryRootResolver.Resolve(parseResult.GetValue(GlobalOptions.RepositoryRoot));
                var layout = new RepositoryLayout(repositoryRoot);
                var manifest = new InstalledVersionsStore(layout.KnowledgeRoot).Load();

                var rows = KnownUmlVersions.All.Select(descriptor =>
                {
                    var entry = manifest.Versions.FirstOrDefault(v => v.Version == descriptor.Version);
                    return new
                    {
                        descriptor.Version,
                        descriptor.IsCurrent,
                        IsDefault = manifest.Default == descriptor.Version,
                        Fetched = entry?.Fetched ?? false,
                        Generated = entry?.Generated ?? false,
                        SpecGenerated = entry?.SpecGenerated ?? false,
                    };
                }).ToList();

                if (parseResult.GetValue(GlobalOptions.Json))
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    return 0;
                }

                var table = new Table();
                table.AddColumn("Version");
                table.AddColumn("Default");
                table.AddColumn("Fetched");
                table.AddColumn("Generated");
                table.AddColumn("Spec text");

                foreach (var row in rows)
                {
                    table.AddRow(row.Version, row.IsDefault ? "yes" : "", row.Fetched ? "yes" : "", row.Generated ? "yes" : "", row.SpecGenerated ? "yes" : "");
                }

                AnsiConsole.Write(table);

                // The companion OMG XMI specification versions independently of UML - it isn't a per-UML-
                // version fact, so it gets a footer rather than a column on the table above.
                var xmiSpecEntry = manifest.XmiSpecs.FirstOrDefault(x => x.Version == KnownXmiVersions.Current.Version);
                AnsiConsole.MarkupLineInterpolated($"XMI {KnownXmiVersions.Current.Version} specification citation: {(xmiSpecEntry?.Generated == true ? "available" : "not available")}");

                return 0;
            });

            return command;
        }
    }
}

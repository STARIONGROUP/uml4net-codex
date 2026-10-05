// -------------------------------------------------------------------------------------------------
// <copyright file="InspectCommand.cs" company="Starion Group S.A.">
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
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using Spectre.Console;

    using Uml4Net.Sage.Knowledge;
    using Uml4Net.Sage.Tools.Inspection;

    /// <summary>
    /// <c>uml4net-sage inspect &lt;path&gt;</c>: loads a user-supplied <c>.xmi</c>/<c>.uml</c> file via
    /// <c>uml4net.xmi</c> and checks it against the generated metamodel graph.
    /// </summary>
    public static class InspectCommand
    {
        private static readonly Argument<string> PathArgument = new("path") { Description = "Path to the .xmi/.uml file to inspect." };

        private static readonly Option<string[]> PathmapOption = new("--pathmap")
        {
            Description = "Map a pathmap:// URI to a local file or directory, as <uri>=<path> (repeatable). "
                + "A prefix such as pathmap://UML_LIBRARIES mapped to a directory resolves every document under it.",
            Arity = ArgumentArity.ZeroOrMore,
        };

        /// <summary>
        /// Builds the command.
        /// </summary>
        public static Command Build()
        {
            var command = new Command("inspect", "Check a UML model file against the generated metamodel.");
            command.Add(PathArgument);
            command.Add(PathmapOption);
            command.Add(GlobalOptions.RepositoryRoot);
            command.Add(GlobalOptions.Version);
            command.Add(GlobalOptions.Json);

            command.SetAction(parseResult =>
            {
                var version = parseResult.GetValue(GlobalOptions.Version)!;
                var modelPath = parseResult.GetValue(PathArgument)!;
                var layout = new RepositoryLayout(RepositoryRootResolver.Resolve(parseResult.GetValue(GlobalOptions.RepositoryRoot)));
                var metamodelJsonPath = Path.Combine(layout.KnowledgeDirectoryFor(version), "metamodel", "metamodel.json");

                if (!File.Exists(metamodelJsonPath))
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]UML {version} has not been generated yet.[/] Run 'uml4net-sage generate --version {version}' first.");
                    return 1;
                }

                if (!File.Exists(modelPath))
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]File not found:[/] {modelPath}");
                    return 1;
                }

                var pathMaps = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var mapping in parseResult.GetValue(PathmapOption) ?? [])
                {
                    var separator = mapping.IndexOf('=');
                    if (separator <= 0 || separator == mapping.Length - 1)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[red]Invalid --pathmap '{mapping}':[/] expected <uri>=<path>, e.g. pathmap://UML_LIBRARIES=./libraries");
                        return 1;
                    }

                    pathMaps[mapping[..separator]] = mapping[(separator + 1)..];
                }

                var report = XmiInspector.Inspect(modelPath, metamodelJsonPath, version, pathMaps);

                if (parseResult.GetValue(GlobalOptions.Json))
                {
                    AnsiConsole.Profile.Out.Writer.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                    return report.Findings.Count > 0 ? 1 : 0;
                }

                if (report.Findings.Count == 0)
                {
                    AnsiConsole.MarkupLineInterpolated($"[green]No issues found[/] in {modelPath} against UML {version}.");
                    return 0;
                }

                var table = new Table();
                table.AddColumn("Line");
                table.AddColumn("Severity");
                table.AddColumn("Category");
                table.AddColumn("Element");
                table.AddColumn("Message");

                foreach (var finding in report.Findings)
                {
                    table.AddRow(
                        finding.Line?.ToString(CultureInfo.InvariantCulture) ?? "",
                        finding.Severity == "error" ? "[red]error[/]" : "[yellow]warning[/]",
                        finding.Category,
                        (finding.ElementXmiId ?? "").EscapeMarkup(),
                        finding.Message.EscapeMarkup());
                }

                AnsiConsole.Write(table);
                return 1;
            });

            return command;
        }
    }
}

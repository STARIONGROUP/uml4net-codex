// -------------------------------------------------------------------------------------------------
// <copyright file="XmiInspector.cs" company="Starion Group S.A.">
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

namespace Uml4Net.Sage.Tools.Inspection
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Microsoft.Extensions.Logging;

    using uml4net;
    using uml4net.CommonStructure;
    using uml4net.Extensions;
    using uml4net.Packages;
    using uml4net.xmi;
    using uml4net.xmi.Readers;

    using Uml4Net.Sage.MetamodelGen.Generators;

    /// <summary>
    /// Loads a user-supplied <c>.xmi</c>/<c>.uml</c> file via <c>uml4net.xmi</c> - the same reader the
    /// knowledge base itself is built with - and checks it against the generated metamodel.
    /// </summary>
    /// <remarks>
    /// Checks, in order:
    /// <list type="number">
    /// <item>A plain XML pass (<see cref="XmiDocumentScanner"/>) flags every element whose <c>xmi:type</c> is an
    /// abstract or unknown metaclass, and every duplicate <c>xmi:id</c>, with line numbers - even when
    /// <c>uml4net.xmi</c> then cannot read the file.</item>
    /// <item>The file is read with <see cref="IXmiReaderSettings.ThrowOnUnresolvedReferences"/>, so every
    /// reference the reader could not resolve comes back as a structured
    /// <see cref="XmiReferenceResolutionFailure"/> (element, property, identifier, kind). Only when there are
    /// failures is it read a second time, without throwing, to get the model for the next checks.</item>
    /// <item>Every element read is checked against the lower and finite upper bounds of the non-derived
    /// features its metaclass requires (from <c>metamodel.json</c>).</item>
    /// <item>Every stereotype application is checked for a resolved stereotype and target, and for a target
    /// whose metaclass the stereotype (or one of its generals) actually extends.</item>
    /// <item>Any remaining reader warning/error is surfaced as a <c>reader-diagnostic</c>.</item>
    /// </list>
    /// </remarks>
    public static class XmiInspector
    {
        private const string PathmapScheme = "pathmap://";

        private const string Error = "error";

        private const string Warning = "warning";

        /// <summary>
        /// Log categories whose messages the structured checks already report (unresolved references and
        /// stereotype application resolution), so they are not repeated as <c>reader-diagnostic</c>s.
        /// </summary>
        private static readonly string[] StructurallyReportedLogCategories =
        [
            "uml4net.Assembler",
            "uml4net.xmi.ReferenceResolver.ExternalReferenceResolver",
            "uml4net.xmi.Readers.StereoTypeApplicationResolver",
        ];

        /// <summary>
        /// Inspects <paramref name="modelPath"/> against the metamodel graph at <paramref name="metamodelJsonPath"/>.
        /// </summary>
        /// <param name="modelPath">The model file to inspect.</param>
        /// <param name="metamodelJsonPath">The generated <c>metamodel.json</c> to check against.</param>
        /// <param name="umlVersion">The UML version <paramref name="metamodelJsonPath"/> describes.</param>
        /// <param name="pathMaps">
        /// <c>pathmap://</c> mappings: a key naming a document (<c>pathmap://LIB/types.uml</c>) maps to that file; a
        /// key naming a prefix (<c>pathmap://LIB</c>) whose value is a directory maps every referenced document
        /// under that prefix into the directory.
        /// </param>
        /// <returns>The inspection report.</returns>
        public static InspectionReport Inspect(string modelPath, string metamodelJsonPath, string umlVersion, IReadOnlyDictionary<string, string>? pathMaps = null)
        {
            var facts = MetamodelFacts.Load(metamodelJsonPath);
            var scan = XmiDocumentScanner.Scan(modelPath);
            var findings = new List<InspectionFinding>();

            CheckDeclaredMetaclasses(scan, facts, findings);
            CheckDuplicateXmiIds(scan, findings);

            var resolvedPathMaps = ExpandPathMaps(pathMaps, scan.PathmapDocuments);
            var documentName = Path.GetFileName(modelPath);

            XmiReaderResult? result = null;
            IReadOnlyList<XmiReferenceResolutionFailure> failures = [];
            var loggerProvider = new CapturingLoggerProvider();

            try
            {
                try
                {
                    result = Read(modelPath, resolvedPathMaps, throwOnUnresolvedReferences: true, loggerProvider);
                }
                catch (UnresolvedReferencesException exception)
                {
                    failures = exception.Failures;
                    loggerProvider = new CapturingLoggerProvider();
                    result = Read(modelPath, resolvedPathMaps, throwOnUnresolvedReferences: false, loggerProvider);
                }
            }
            catch (Exception exception)
            {
                findings.Add(new InspectionFinding(
                    Error,
                    "read-aborted",
                    null,
                    $"uml4net.xmi could not read the model ({exception.GetType().Name}: {exception.Message}), so only the xmi:type and xmi:id checks ran."));
            }

            var ownFailures = failures
                .Where(failure => string.Equals(failure.DocumentName, documentName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            ReportResolutionFailures(ownFailures, scan, resolvedPathMaps, findings);

            if (result is not null)
            {
                var failedProperties = ownFailures
                    .Select(failure => (failure.ElementXmiId, failure.PropertyName))
                    .ToHashSet();

                foreach (var element in result.DocumentRootElements.OfType<IElement>().SelectMany(Descendants))
                {
                    CheckMultiplicities(element, facts, failedProperties, scan, findings);
                }

                CheckStereotypeApplications(result, facts, scan, findings);
            }

            foreach (var entry in loggerProvider.Entries.Where(IsUnreportedDiagnostic))
            {
                findings.Add(new InspectionFinding(entry.Level == LogLevel.Error ? Error : Warning, "reader-diagnostic", null, entry.Message));
            }

            var ordered = findings
                .OrderBy(finding => finding.Line ?? int.MaxValue)
                .ThenBy(finding => finding.Category, StringComparer.Ordinal)
                .ThenBy(finding => finding.ElementXmiId, StringComparer.Ordinal)
                .ToList();

            return new InspectionReport(modelPath, umlVersion, ordered);
        }

        private static XmiReaderResult Read(string modelPath, IReadOnlyDictionary<string, string> pathMaps, bool throwOnUnresolvedReferences, CapturingLoggerProvider loggerProvider)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider).SetMinimumLevel(LogLevel.Warning));

            var reader = XmiReaderBuilder.Create()
                .UsingSettings(settings =>
                {
                    settings.LocalReferenceBasePath = Path.GetDirectoryName(Path.GetFullPath(modelPath)) ?? ".";
                    settings.UseStrictReading = false;
                    settings.ThrowOnUnresolvedReferences = throwOnUnresolvedReferences;

                    foreach (var (key, value) in pathMaps)
                    {
                        settings.PathMaps[key] = value;
                    }
                })
                .WithLogger(loggerFactory)
                .Build();

            using (reader)
            {
                return reader.Read(Path.GetFullPath(modelPath));
            }
        }

        private static void CheckDeclaredMetaclasses(XmiDocumentScan scan, MetamodelFacts facts, List<InspectionFinding> findings)
        {
            foreach (var element in scan.Elements)
            {
                if (!facts.IsKnown(element.Metaclass))
                {
                    findings.Add(new InspectionFinding(
                        Error,
                        "unknown-metaclass",
                        element.XmiId,
                        $"xmi:type '{element.Metaclass}' is not a metaclass of the UML metamodel.",
                        element.Line));
                }
                else if (facts.IsAbstract(element.Metaclass))
                {
                    findings.Add(new InspectionFinding(
                        Error,
                        "abstract-instantiation",
                        element.XmiId,
                        $"xmi:type '{element.Metaclass}' is abstract in the UML metamodel and cannot be instantiated directly.",
                        element.Line));
                }
            }
        }

        private static void CheckDuplicateXmiIds(XmiDocumentScan scan, List<InspectionFinding> findings)
        {
            foreach (var (xmiId, lines) in scan.XmiIdLines.Where(entry => entry.Value.Count > 1))
            {
                findings.Add(new InspectionFinding(
                    Error,
                    "duplicate-xmi-id",
                    xmiId,
                    $"xmi:id '{xmiId}' is declared {lines.Count} times (lines {string.Join(", ", lines)}); a reader keeps only the first.",
                    lines[1]));
            }
        }

        private static void ReportResolutionFailures(
            IReadOnlyList<XmiReferenceResolutionFailure> failures,
            XmiDocumentScan scan,
            IReadOnlyDictionary<string, string> pathMaps,
            List<InspectionFinding> findings)
        {
            foreach (var failure in failures)
            {
                var hint = string.Empty;
                if (failure.Identifier?.StartsWith(PathmapScheme, StringComparison.Ordinal) == true)
                {
                    var document = failure.Identifier.Split('#')[0];
                    hint = pathMaps.TryGetValue(document, out var mappedPath)
                        ? $" (mapped to '{mappedPath}')"
                        : " (no --pathmap was given for it)";
                }

                var subject = $"{failure.ElementXmiType} '{failure.ElementXmiId}'";
                var message = failure.Kind switch
                {
                    XmiReferenceResolutionFailureKind.NotFound => $"{subject}: {failure.PropertyName} references '{failure.Identifier}', which could not be found{hint}.",
                    XmiReferenceResolutionFailureKind.UnexpectedType => $"{subject}: {failure.PropertyName} references '{failure.Identifier}', whose type is not allowed for that property.",
                    XmiReferenceResolutionFailureKind.AlreadyOwned => $"{subject}: {failure.PropertyName} claims to own '{failure.Identifier}', which another element already owns.",
                    XmiReferenceResolutionFailureKind.MultiplicityExceeded => $"{subject}: {failure.PropertyName} holds more values than its multiplicity allows, so '{failure.Identifier}' was dropped.",
                    _ => $"{subject}: {failure.PropertyName} -> '{failure.Identifier}' ({failure.Kind}).",
                };

                findings.Add(new InspectionFinding(
                    Error,
                    failure.Kind == XmiReferenceResolutionFailureKind.NotFound ? "unresolved-reference" : "invalid-reference",
                    failure.ElementXmiId,
                    message,
                    LineOf(scan, failure.ElementXmiId)));
            }
        }

        private static void CheckMultiplicities(
            IElement element,
            MetamodelFacts facts,
            HashSet<(string ElementXmiId, string PropertyName)> failedProperties,
            XmiDocumentScan scan,
            List<InspectionFinding> findings)
        {
            if (element is not IXmiElement xmiElement || element.MetaclassInterface is null)
            {
                return;
            }

            var metaclass = element.MetaclassInterface.Name[1..];

            foreach (var feature in facts.RequiredFeaturesOf(metaclass))
            {
                if (failedProperties.Contains((xmiElement.XmiId, feature.Name)) || !TryCountValues(element, feature.Name, out var count))
                {
                    continue;
                }

                if (count >= feature.Lower && count <= feature.UpperBound)
                {
                    continue;
                }

                findings.Add(new InspectionFinding(
                    Error,
                    "multiplicity-violation",
                    xmiElement.XmiId,
                    $"{Describe(element, metaclass, xmiElement.XmiId)}: {feature.OwnerName}::{feature.Name} [{feature.Lower}..{feature.Upper}] has {count} value(s).",
                    LineOf(scan, xmiElement.XmiId)));
            }
        }

        /// <summary>
        /// Describes an element for a finding message, e.g. <c>Class 'Car' (Car)</c>, or <c>Generalization 'Car-gen'</c>
        /// for an unnamed one.
        /// </summary>
        private static string Describe(IElement element, string metaclass, string xmiId)
        {
            var name = (element as INamedElement)?.Name;
            return string.IsNullOrEmpty(name) ? $"{metaclass} '{xmiId}'" : $"{metaclass} '{name}' ({xmiId})";
        }

        /// <summary>
        /// Counts the values <paramref name="element"/> holds for the metamodel feature <paramref name="featureName"/>,
        /// via the uml4net property of the same (Pascal-cased) name. A value-typed property always holds a value and
        /// is not counted.
        /// </summary>
        private static bool TryCountValues(IElement element, string featureName, out int count)
        {
            count = 0;
            PropertyInfo? property;

            try
            {
                property = element.GetType().GetProperty(char.ToUpperInvariant(featureName[0]) + featureName[1..], BindingFlags.Public | BindingFlags.Instance);
            }
            catch (AmbiguousMatchException)
            {
                return false;
            }

            if (property is null || property.PropertyType.IsValueType)
            {
                return false;
            }

            count = property.GetValue(element) switch
            {
                null => 0,
                string => 1,
                IEnumerable values => values.Cast<object?>().Count(item => item is not null),
                _ => 1,
            };

            return true;
        }

        private static void CheckStereotypeApplications(XmiReaderResult result, MetamodelFacts facts, XmiDocumentScan scan, List<InspectionFinding> findings)
        {
            foreach (var application in result.XmiRoot?.StereoTypeApplications ?? [])
            {
                var line = LineOf(scan, application.XmiId);

                if (application.ExtendedElement is null)
                {
                    findings.Add(new InspectionFinding(
                        Error,
                        "stereotype-target-unresolved",
                        application.XmiId,
                        $"«{application.StereoTypeName}» is applied to '{application.ElementIdentifier}' (base_{application.MetaClass}), which could not be found.",
                        line));
                    continue;
                }

                if (application.Stereotype is null)
                {
                    findings.Add(new InspectionFinding(
                        Warning,
                        "stereotype-unresolved",
                        application.XmiId,
                        $"«{application.StereoTypeName}» from profile '{application.ProfileName}' could not be resolved, so its application to '{application.ElementIdentifier}' was not checked - is the profile next to the model, or mapped with --pathmap?",
                        line));
                    continue;
                }

                if (application.ExtendedElement is not IElement target || target.MetaclassInterface is null)
                {
                    continue;
                }

                var targetMetaclass = target.MetaclassInterface.Name[1..];
                var allowed = ExtendedMetaclassesOf(application.Stereotype);

                if (allowed.Count > 0 && !allowed.Any(metaclass => facts.Conforms(targetMetaclass, metaclass)))
                {
                    findings.Add(new InspectionFinding(
                        Error,
                        "stereotype-misapplied",
                        application.XmiId,
                        $"«{application.Stereotype.Name}» extends {string.Join(", ", allowed)}, but is applied to {targetMetaclass} '{application.ElementIdentifier}'.",
                        line));
                }
            }
        }

        /// <summary>
        /// Gets the metaclasses <paramref name="stereotype"/> may be applied to: those it or any of its generals extends.
        /// </summary>
        private static IReadOnlyList<string> ExtendedMetaclassesOf(IStereotype stereotype)
        {
            var stereotypes = new List<IStereotype> { stereotype };
            stereotypes.AddRange(stereotype.QueryAllGeneralClassifiers().OfType<IStereotype>());

            return stereotypes
                .SelectMany(StereotypeFileGenerator.ExtensionsOf)
                .Select(extension => extension.Metaclass)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        private static Dictionary<string, string> ExpandPathMaps(IReadOnlyDictionary<string, string>? pathMaps, IReadOnlyList<string> referencedDocuments)
        {
            var expanded = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (key, value) in pathMaps ?? new Dictionary<string, string>())
            {
                if (!Directory.Exists(value))
                {
                    expanded[key] = Path.GetFullPath(value);
                    continue;
                }

                var prefix = key.TrimEnd('/') + "/";
                foreach (var document in referencedDocuments.Where(document => document.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    expanded[document] = Path.GetFullPath(Path.Combine(value, document[prefix.Length..].Replace('/', Path.DirectorySeparatorChar)));
                }
            }

            return expanded;
        }

        private static bool IsUnreportedDiagnostic(CapturedLogEntry entry)
        {
            // duplicate ids are reported, with line numbers, from the document scan; the reader also logs those of
            // referenced documents (OMG's own UML.xmi has some), which are not the inspected model's problem
            return !StructurallyReportedLogCategories.Contains(entry.Category, StringComparer.Ordinal)
                && !entry.Message.Contains("is not unique within the document", StringComparison.Ordinal);
        }

        private static int? LineOf(XmiDocumentScan scan, string? xmiId)
        {
            return xmiId is not null && scan.XmiIdLines.TryGetValue(xmiId, out var lines) ? lines[0] : null;
        }

        private static IEnumerable<IElement> Descendants(IElement element)
        {
            yield return element;

            foreach (var owned in element.OwnedElement)
            {
                foreach (var descendant in Descendants(owned))
                {
                    yield return descendant;
                }
            }
        }
    }
}

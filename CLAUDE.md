# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working in this repository.

## What this repo is

Two things in one repository:

1. **A Claude Code plugin** (`.claude-plugin/`, `skills/`, `agents/`, `commands/`,
   `hooks/native/`) that makes Claude knowledgeable about the OMG UML 2.5.1 specification.
2. **The generation toolchain** (`tools/`) that builds the plugin's knowledge base from OMG's own
   XMI and PDF files, fetched fresh on each user's machine. None of this toolchain ships with the
   installed plugin - see `tools/README.md`.

This project is modeled on [mycelium-hypha](https://github.com/mycelium-cmbse/mycelium-hypha) (the
same pattern for KerML/SysML2), adapted for UML's simpler, static-URL, single-vendor situation - no
GitHub-hosted mirror, no rolling release cadence, and (per explicit product decision) PDF extraction
stays Python while everything else is C#.

## Build & test commands

```bash
# .NET (everything except spec-extract)
dotnet restore uml4net-sage.slnx
dotnet build uml4net-sage.slnx
dotnet test uml4net-sage.slnx --no-build

# Run a single test project
dotnet test tools/metamodel-gen/Uml4Net.Sage.MetamodelGen.Tests/Uml4Net.Sage.MetamodelGen.Tests.csproj

# Python (tools/spec-extract only)
cd tools/spec-extract
python -m venv .venv
.venv/Scripts/pip install -e ".[dev]"   # .venv/bin/pip on Linux/macOS
.venv/Scripts/pytest -v
.venv/Scripts/ruff check .
.venv/Scripts/ruff format .
```

**Test frameworks**: NUnit 4.x for .NET, pytest for Python. **Target**: `net10.0` for all tooling
(confirm this still matches whatever the sibling `uml4net` repository's tooling projects target
before bumping either).

## Architecture

| Project | Purpose |
|---|---|
| `tools/metamodel-gen/Uml4Net.Sage.MetamodelGen` | Reads UML.xmi/PrimitiveTypes.xmi/StandardProfile.xmi via `uml4net.xmi`, generates `metamodel/` + `standard-profile/` |
| `tools/knowledge-gen/Uml4Net.Sage.Knowledge` | `KnownUmlVersions` registry, `SourceFetcher` (plain HTTPS GETs from omg.org), `PythonSpecExtractRunner` (the C#/Python seam), `DataPackageGenerator`, `InstalledVersionsStore` |
| `tools/sage-cli/Uml4Net.Sage.Tools` | The `uml4net-sage` CLI: `fetch`, `generate`, `versions`, `use`, `remove`, `check`, `inspect` |
| `tools/sage-cli/Uml4Net.Sage.Tools.Hook` | The committed, NativeAOT, dependency-free SessionStart hook |
| `tools/spec-extract` | Python: PDF-to-markdown clause extraction |

### Key types & patterns

- **Reading UML metamodel XMI**: `Uml4Net.Sage.MetamodelGen.XmiModelReader.Read(path, localReferenceBasePath)`
  wraps `uml4net.xmi`'s `XmiReaderBuilder`. UML.xmi/PrimitiveTypes.xmi/StandardProfile.xmi/UMLDI.xmi
  cross-reference each other via **absolute http(s) hrefs** (e.g.
  `http://www.omg.org/spec/UML/20161101/UML.xmi#Class`), not `pathmap://` URIs - `uml4net.xmi`
  resolves these by looking for a same-named local file under `LocalReferenceBasePath`, so as long
  as all four files sit together under their original names, no `PathMaps` entries are needed.
  (`PathMaps`/`pathmap://` still matters for the `inspect` verb's `XmiInspector`, which loads
  arbitrary user-supplied XMI that may use that scheme, e.g. Enterprise Architect/MagicDraw exports.)
- **uml4net.xmi 9.x semantics to keep in mind**: as of 9.0.0 every derived property is implemented
  (no more `NotSupportedException`), so read derived properties directly rather than re-deriving them
  by hand. Behaviours that differ from what you might assume:
  - Optional `[0..1]` value properties are nullable: `IOperation.Lower` is `int?` and, like
    `IOperation.Upper`, is null for an operation with no return parameter (sage reports `0`/`"0"`,
    matching what 8.x returned); `INamedElement.Visibility` is `VisibilityKind?`.
  - `INamedElement.QualifiedName` is null when the element or any enclosing namespace is unnamed.
  - `IProperty.IsComposite` follows the spec strictly (`aggregation == composite`).
  - `IClass.Extension` follows the OCL's `endTypes.allParents()` branch, so a stereotype's `Extension`
    also lists its specializations' extensions - `StereotypeFileGenerator.ExtensionsOf` filters to
    the extensions whose `ExtensionEnd` is typed by that stereotype itself.
- **Why the full CLI is not NativeAOT**: `uml4net.xmi` (reflection-heavy), Spectre.Console and
  System.CommandLine are not trim/AOT-friendly, so the CLI ships as a self-contained (non-AOT) publish
  per runtime identifier (`.github/workflows/release.yml`). Only the dependency-free SessionStart hook
  (`tools/sage-cli/Uml4Net.Sage.Tools.Hook`) is NativeAOT - see `hooks/native/README.md`.
- **`PythonSpecExtractRunner`'s `uv` fallback**: verbatim spec citation (both `uml-spec-citation` and
  `xmi-spec-citation`) shells out to
  `tools/spec-extract` (Python/pdfplumber). It first tries a `.venv` under that directory or a system
  `python`/`python3`; if neither is found, it provisions [`uv`](https://astral.sh/uv) on demand
  (`Uml4Net.Sage.Knowledge.Toolchain.UvProvisioner`, downloaded/checksum-verified from its GitHub
  releases and cached alongside the CLI's own self-fetch cache) and runs `uv run --project
  tools/spec-extract python -m spec_extract ...` instead - `uv` resolves a matching Python and installs
  the project's dependencies into a managed venv itself, so an installed plugin needs neither a
  pre-existing Python nor a provisioned `.venv`. Every failure mode degrades to
  `SpecExtractionOutcome.Skipped` with a reason; it never throws or aborts the rest of `generate`.
- **Determinism of generated artifacts**: every generator sorts with `StringComparer.Ordinal`,
  writes LF line endings (`.gitattributes` pins this), and uses fixed `System.Text.Json` options
  (see `MetamodelJsonGenerator.SerializerOptions`) - checked by
  `MetamodelGeneratorTests.Generate_is_byte_for_byte_deterministic_across_two_independent_runs`,
  which regenerates twice into scratch folders and diffs them byte-for-byte (there's no committed
  baseline to compare against, since nothing generated is committed - see below).

## Committed vs git-ignored (and why)

This is the single most load-bearing convention in the repo, driven entirely by OMG's specification
license (see `NOTICE`): OMG permits informational use of its specification but not redistribution.

**Never committed**: anything under `sources/<version>/` (the raw XMI/PDF files) or
`knowledge/<version>/` (the generated knowledge base, since it's derived from and in the `spec/`
case *is* the OMG text) or `knowledge/installed.json` (purely local machine state). All regenerated
fresh, per machine, by `uml4net-sage fetch` + `generate`.

**Committed**: everything version-independent - `knowledge/registry.json` and the `*.schema.json`
files (they describe uml4net-sage's own output shape, not OMG content), `sources/README.md` and
`knowledge/README.md` (provenance/shape documentation, no OMG text), the plugin's `skills/`/
`agents/`/`commands/`, and `hooks/native/*` binaries (compiled from this repo's own C# source - see
`hooks/native/README.md` for why binaries are the one exception to "nothing generated is
committed").

**No committed test fixtures derived from OMG content**: `Uml4Net.Sage.MetamodelGen.Tests` uses
small, hand-authored XMI describing a fictional toy metamodel (`Widget`/`Gadget`/`Sample`) under its
own `Fixtures/` directory, specifically to sidestep any question about whether OMG's XMI may be
legally redistributed as test data (its specification *license* only clearly covers the *text*, not
the machine-readable files, and this project doesn't assume otherwise without confirming).

## Knowledge-base shape

See `knowledge/README.md` for the full generated-file layout and the three-tier provenance model
(NORMATIVE / MODEL / DERIVED) every skill answer should be tagged with.

## spec-extract pipeline

Python, ported from mycelium-hypha's own `tools/spec-extract` (same layered
reader → layout → clauses → normative → markdown pipeline), adapted for OMG UML's PDF formatting.
See `tools/spec-extract/README.md` for the pipeline stages, the `pdfplumber`-over-PyMuPDF licensing
rationale, and known real-world extraction limitations (figure-embedded text, symbol-font bullets).

## Conventions

Pulled from the sibling `uml4net` repository's own `CLAUDE.md` for ecosystem consistency:

- 4 spaces indentation, no tabs; no `_` prefix on member names; use `this.` for instance members.
- Use `var` unless the type is ambiguous; use C# type aliases (`int`, `string`, not `Int32`, `String`).
- Always wrap blocks in curly braces, even single-line; no `#region` directives.
- `using` statements go inside the namespace; `System.*` first, then alphabetical.
- All public members require XML documentation (`///`).
- Apache 2.0 copyright header on every `.cs` file (see any existing file for the exact template).
- Default branch: `development` (never work directly on `master`). Create feature branches from
  `development`; rebase before submitting PRs.

## Local do's and don'ts

- Python exists in exactly one place (`tools/spec-extract`) for exactly one reason (PDF text
  extraction genuinely needs it - see that project's README). Don't introduce Python anywhere else
  in this repo; everything else is C#.
- Don't add a fixture-XMI-redistribution workaround (committing real OMG XMI as test data) without
  first confirming OMG's actual position on redistributing machine-readable files, not just the
  specification text - see "Committed vs git-ignored" above.

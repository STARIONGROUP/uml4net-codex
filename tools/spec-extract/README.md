# spec-extract

PDF-to-markdown clause extraction for the OMG UML 2.5.1 specification. This is a
direct port of [mycelium-hypha](https://github.com/mycelium-cmbse/mycelium-hypha)'s
`tools/spec-extract`, adapted for OMG UML's PDF formatting (different heading/numbering
scheme and running header/footer text than KerML/SysML's spec). It is the one Python
component in an otherwise all-C# repository — see the root `CLAUDE.md` for why.

## Pipeline

Five pure, independently-tested stages, each in its own module under `src/spec_extract/`:

1. **`pdf_reader.py`** — wraps [pdfplumber](https://github.com/jsvine/pdfplumber) and extracts
   positioned words/letters per page. No layout logic here.
2. **`layout.py`** — reconstructs reading order (Y-clustering into lines, left-to-right within
   a line) and strips running headers/footers, including a page-number footer that changes on
   every page.
3. **`clauses.py`** — detects clause headings via a successor-numbering heuristic (a heading
   number must be a plausible child/sibling of the previous one), skipping the Table of Contents.
   Lettered annexes ("Annex B", "B.5.2 ...") continue the sequence after the last numbered clause;
   an annex's "(normative)"/"(informative)" designation becomes the `normative` flag of the annex
   and all of its subclauses.
4. **`normative.py`** — groups a clause's lines into paragraphs and tags each as normative
   (contains "shall"/"must") or informative (`NOTE`/`EXAMPLE`). A numbered clause is tagged
   normative when any of its paragraphs is; an annex clause takes its annex's designation instead.
5. **`markdown.py`** — renders one clause to markdown with YAML front matter.

`pipeline.py` orchestrates all five across a whole PDF and writes:

```
<output_dir>/clauses/<number>-<slug>.md   one file per clause
<output_dir>/index.json                   array of {clause, title, pages, normative, file}
<output_dir>/index.md                     human-readable table of the same
```

## Dev workflow

Test-driven, not a distributed CLI, same as hypha's own `spec-extract` — `pytest` is the primary
way to exercise and verify the pipeline:

```bash
python -m venv .venv
.venv/Scripts/pip install -e ".[dev]"   # .venv/bin/pip on Linux/macOS
.venv/Scripts/pytest -v
.venv/Scripts/ruff check .
.venv/Scripts/ruff format .
```

Unit tests use hand-built synthetic data (including a minimal, hand-written test PDF built by
`tests/pdf_fixtures.py` — not derived from any OMG or third-party document). One test
(`test_extract_real_specification_has_plausible_clause_count`, marked `@pytest.mark.live`) runs
the full pipeline against a real OMG UML spec PDF, but only when `UML_SPEC_PDF_PATH` points at a
locally-supplied copy — it is never fetched or committed by the test itself, and it asserts only
structural properties (clause count, a known clause-number prefix), never verbatim spec text.

A thin, non-interactive entry point exists so the `uml4net-sage` C# CLI's `generate` verb can
invoke this pipeline as a subprocess for its `spec` step:

```bash
python -m spec_extract extract --pdf <path-to-pdf> --out <output-dir> [--document UML] [--version 2.5.1]
```

`PythonSpecExtractRunner` runs this two ways, in order:

1. A `.venv` under this directory (`.venv/Scripts/python.exe` / `.venv/bin/python`) if present, else a
   bare `python`/`python3` on `PATH` - the maintainer dev workflow above.
2. If neither is found (the interpreter itself can't be started), **or** one is found but exits with a
   Python `ModuleNotFoundError` (i.e. `pdfplumber` or `spec_extract` itself isn't installed into it -
   e.g. a bare system Python with nothing `pip install`ed), it provisions [`uv`](https://astral.sh/uv)
   on demand (downloaded and checksum-verified from its GitHub releases, cached under the same local
   app-data folder the CLI's own self-fetch hook uses) and runs `uv run --project tools/spec-extract
   python -m spec_extract ...`. `uv` resolves or fetches a matching Python itself and installs this
   project's dependencies into a managed venv, so verbatim spec citation works for an installed plugin
   too - no pre-existing Python or provisioned `.venv` needed, just network access the first time.
   `uv.lock` (committed) pins that resolution, the same way `Directory.Packages.props` pins exact
   versions on the C# side.
3. If `uv` itself can't be provisioned (offline, or an unsupported platform) or its run also fails,
   `generate` reports the skip with an explicit reason - naming `pdfplumber` specifically when that
   was the cause, with the exact `pip install -e tools/spec-extract` command to fix it - and states
   plainly that metamodel/Standard Profile lookups are unaffected either way; only verbatim spec
   citation stays unavailable until it's resolved.

## Known limitations

Verified against the real OMG UML 2.5.1 specification PDF: ordinary prose clauses (e.g. clause 1,
"Scope") extract cleanly and accurately. Two lower-priority rough edges remain, both cosmetic
rather than semantic:

- **Figure-embedded text renders as garbled fragments.** Clauses whose page is dominated by a UML
  diagram (class boxes, association labels) pick up the diagram's positioned text as if it were
  prose, since diagram label layout doesn't follow normal reading order. The clause's front matter
  and heading are still correct; only the body text of that specific clause is affected.
- **Symbol-font bullets and some composite headers/footers aren't perfectly stripped.** A bullet
  glyph drawn from a symbol font (e.g. Wingdings) can decode as a mojibake character instead of
  `-`/`•`, and a running footer that combines a fixed title with a per-page number (rather than a
  bare page number) can occasionally survive header/footer stripping.

Neither affects clause numbering, titles, or the normative/informative classification - only the
verbatim body text of the affected clauses. The `uml-spec-citation` and `xmi-spec-citation` skills
(this pipeline extracts both the OMG UML and OMG XMI specifications) should treat unusually
garbled-looking body text with appropriate skepticism.

## Licensing & OMG terms

This package never fetches, stores, or commits the OMG UML specification PDF, and its output
(the extracted clause markdown, which is the full verbatim specification text) is git-ignored —
see the root `CLAUDE.md` ("Committed vs git-ignored") and `NOTICE`.

**PDF library**: [pdfplumber](https://github.com/jsvine/pdfplumber) (MIT) is used instead of
PyMuPDF, which is AGPL-3.0/commercially licensed — an awkward fit even for a non-distributed dev
tool, matching hypha's own reasoning for the same choice.

"""Stage 6: orchestrate stages 1-5 across a whole PDF and write the knowledge-base output.

Output layout (matches the uml4net-sage knowledge base convention):
    <output_dir>/clauses/<number>-<slug>.md   one file per clause
    <output_dir>/index.json                   array of {clause, title, document, pages, normative, file}
    <output_dir>/index.md                     human-readable table of the same

This module is deliberately not a distributed CLI in the usual sense - see __main__.py
for the thin entry point that exists only so the C# `generate` verb can invoke it.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from spec_extract.clauses import detect_clauses
from spec_extract.layout import reconstruct_pages
from spec_extract.markdown import render_clause_markdown
from spec_extract.models import Clause, clause_number_key
from spec_extract.normative import split_normative
from spec_extract.pdf_reader import read_positioned_pages


@dataclass
class ExtractionResult:
    """What one run of the pipeline produced, for callers/tests to inspect."""

    clauses: list[Clause]
    output_dir: Path


def _write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(content)


def _write_index(output_dir: Path, clauses: list[Clause], *, document: str) -> None:
    # `document` is included on every row (not just in each clause's own front matter) so that two
    # sibling knowledge trees extracted from different OMG documents (e.g. the UML and XMI
    # specifications) never produce byte-shape-identical rows an agent could ground in the wrong one.
    rows = [
        {
            "clause": clause.number,
            "title": clause.title,
            "document": document,
            "pages": f"{clause.page_start}"
            if clause.page_start == clause.page_end
            else f"{clause.page_start}-{clause.page_end}",
            "normative": clause.is_normative,
            "file": f"clauses/{clause.file_name}",
        }
        for clause in clauses
    ]
    _write_text(output_dir / "index.json", json.dumps(rows, indent=2, sort_keys=True) + "\n")

    lines = ["| Clause | Title | Document | Pages | Normative | File |", "|---|---|---|---|---|---|"]
    for row in rows:
        cells = [row["clause"], row["title"], row["document"], row["pages"], row["normative"], row["file"]]
        lines.append("| " + " | ".join(str(cell) for cell in cells) + " |")
    _write_text(output_dir / "index.md", "\n".join(lines) + "\n")


def extract_document(
    pdf_path: str | Path, output_dir: str | Path, *, document: str = "UML", version: str = "2.5.1"
) -> ExtractionResult:
    """Run the full pipeline against `pdf_path`, writing clause markdown + indexes into `output_dir`."""
    output_dir = Path(output_dir)

    positioned_pages = read_positioned_pages(pdf_path)
    reconstructed_pages = reconstruct_pages(positioned_pages)
    clauses = detect_clauses(reconstructed_pages)
    for clause in clauses:
        split_normative(clause)

    clauses = sorted(clauses, key=lambda clause: clause_number_key(clause.number))

    for clause in clauses:
        markdown = render_clause_markdown(clause, document=document, version=version)
        _write_text(output_dir / "clauses" / clause.file_name, markdown)

    _write_index(output_dir, clauses, document=document)

    return ExtractionResult(clauses=clauses, output_dir=output_dir)

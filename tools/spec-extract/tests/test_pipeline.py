import json
import os
from pathlib import Path

import pytest

from spec_extract.pipeline import extract_document
from tests.pdf_fixtures import build_simple_pdf


def test_extract_document_writes_clause_files_and_indexes(tmp_path: Path) -> None:
    pdf_path = tmp_path / "sample.pdf"
    pdf_path.write_bytes(
        build_simple_pdf(
            [
                "1 Scope",
                "This document shall define scope.",
                "It has more than one sentence.",
                "1.1 Overview",
                "This is the overview text.",
                "It also has more than one sentence.",
            ],
            start_y=700,
            line_height=20,
            sizes=[14.0, 10.0, 10.0, 14.0, 10.0, 10.0],
        )
    )
    output_dir = tmp_path / "spec"

    result = extract_document(pdf_path, output_dir, document="UML", version="2.5.1")

    assert [clause.number for clause in result.clauses] == ["1", "1.1"]

    scope_file = output_dir / "clauses" / "1-scope.md"
    overview_file = output_dir / "clauses" / "1.1-overview.md"
    assert scope_file.exists()
    assert overview_file.exists()
    assert "shall" in scope_file.read_text(encoding="utf-8")

    index = json.loads((output_dir / "index.json").read_text(encoding="utf-8"))
    assert [row["clause"] for row in index] == ["1", "1.1"]
    assert index[0]["normative"] is True
    assert index[1]["normative"] is False
    assert all(row["document"] == "UML" for row in index)

    assert (output_dir / "index.md").exists()
    assert "Document" in (output_dir / "index.md").read_text(encoding="utf-8")


def test_extract_document_threads_the_document_name_into_front_matter_and_index(tmp_path: Path) -> None:
    """Two documents extracted into sibling knowledge trees (e.g. UML and XMI) must never produce
    byte-shape-identical rows an agent could ground an answer in the wrong corpus from."""
    pdf_path = tmp_path / "sample.pdf"
    pdf_path.write_bytes(
        build_simple_pdf(
            ["1 Scope", "This document shall define scope.", "It has more than one sentence."],
            start_y=700,
            line_height=20,
            sizes=[14.0, 10.0, 10.0],
        )
    )
    output_dir = tmp_path / "xmi-spec"

    extract_document(pdf_path, output_dir, document="XMI", version="2.5.1")

    index = json.loads((output_dir / "index.json").read_text(encoding="utf-8"))
    assert index[0]["document"] == "XMI"

    clause_markdown = (output_dir / "clauses" / "1-scope.md").read_text(encoding="utf-8")
    assert 'document: "XMI"' in clause_markdown


@pytest.mark.live
def test_extract_real_specification_has_plausible_clause_count() -> None:
    """Structural-only check against a real, locally-supplied OMG UML spec PDF.

    Never fetches or asserts on verbatim text (that would itself be a redistribution
    risk in test history/CI artifacts) - only structural properties.
    """
    pdf_path = os.environ.get("UML_SPEC_PDF_PATH")
    if not pdf_path:
        pytest.skip("UML_SPEC_PDF_PATH not set - this test only runs against a locally-supplied real spec PDF")

    result = extract_document(pdf_path, Path(pdf_path).parent / "spec-extract-output")

    assert len(result.clauses) > 50
    numbers = {clause.number for clause in result.clauses}
    assert "9.3.2" in numbers or any(number.startswith("9.") for number in numbers)


@pytest.mark.live
def test_extract_real_xmi_specification_has_plausible_clause_count() -> None:
    """Structural-only check against a real, locally-supplied OMG XMI spec PDF.

    Thresholds are far lower than the UML test above: the XMI 2.5.1 spec is a ~120-page document
    with 10 top-level clauses, not UML's ~800-page, 20-top-level-clause one - see the issue #9
    validation spike results (141 clauses extracted, clause 7.10 "Linking" present) for the numbers
    this threshold is based on.
    """
    pdf_path = os.environ.get("XMI_SPEC_PDF_PATH")
    if not pdf_path:
        pytest.skip("XMI_SPEC_PDF_PATH not set - this test only runs against a locally-supplied real spec PDF")

    result = extract_document(pdf_path, Path(pdf_path).parent / "spec-extract-output", document="XMI", version="2.5.1")

    assert len(result.clauses) > 100
    numbers = {clause.number for clause in result.clauses}
    assert "7.10" in numbers


@pytest.mark.live
def test_extract_real_xmi_specification_extracts_its_annexes_as_their_own_clauses() -> None:
    """Regression test for issue #26: lettered annex headings ("Annex B", "B.2 ...") used to be
    folded into the last numbered clause (10.4). Asserts clause numbers, titles and page ranges only -
    never body text."""
    pdf_path = os.environ.get("XMI_SPEC_PDF_PATH")
    if not pdf_path:
        pytest.skip("XMI_SPEC_PDF_PATH not set - this test only runs against a locally-supplied real spec PDF")

    result = extract_document(pdf_path, Path(pdf_path).parent / "spec-extract-output", document="XMI", version="2.5.1")

    clauses = {clause.number: clause for clause in result.clauses}
    assert clauses["B.2"].title == "Constraints and Tag Equivalents"
    assert clauses["10.4"].page_end < clauses["A"].page_start
    assert clauses["A"].is_normative is False
    assert clauses["B"].is_normative is True
    assert clauses["B.5.2"].is_normative is True

    index = json.loads((result.output_dir / "index.json").read_text(encoding="utf-8"))
    indexed = [row["clause"] for row in index]
    assert indexed.index("10.4") < indexed.index("A") < indexed.index("B") < indexed.index("B.2")

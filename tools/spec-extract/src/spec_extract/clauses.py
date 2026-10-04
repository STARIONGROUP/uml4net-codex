"""Stage 3: skip the Table of Contents, then detect clause headings via a font-size-gated,
successor-numbering heuristic.

Skipping the Table of Contents (however many pages long, however irregularly it numbers annexes -
"Annex A:", "B.7.3 UMLClassDiagram [Class]", ...) is done by looking for pages dense with
dotted-leader entries ("... NN" trailing a page number) - a TOC's structural signature - rather
than by pattern-matching the entry text itself, which real annexes defeat.

Once inside the body, a line is treated as a clause heading candidate only when it (a) starts with
a dotted number (e.g. "9.3.2 Classifier") *and* (b) is set in a visibly larger font than the
document's body text - real headings in a typeset specification are styled distinctly from prose,
and without this gate a long technical document's numbered lists, table rows, and cross-references
produce a flood of false positives. A candidate is accepted as an actual heading only when its
number is also a plausible successor of the previously accepted heading number in a depth-first
traversal of the section tree (a child, a sibling, or a sibling of some ancestor).

Annexes follow the last numbered clause and are numbered by letter: an "Annex B" heading (its title
either on the same line, "Annex B: UML Diagram Interchange", or on the next heading-sized line, as
the XMI specification sets it), then lettered subclauses ("B.1 Overview", "B.5.2 Property
Elements"). They are detected the same way, with each letter mapped to a number past every numbered
clause (see `clause_number_key`) so the successor check also covers the step from the last numbered
clause to "Annex A". The "(normative)"/"(informative)" line under an annex title is taken as that
annex's designation, for the annex and all of its subclauses, rather than kept as body text.
"""

from __future__ import annotations

import re
import statistics

from spec_extract.models import ANNEX_KEY_BASE, Clause, Line, ReconstructedPage, clause_number_key

_HEADING = re.compile(r"^(\d+(?:\.\d+)*|[A-Z](?:\.\d+)+)\s+(\S.*)$")
_ANNEX_HEADING = re.compile(r"^Annex\s+([A-Z])(?:\s*[:.\-\u2013\u2014]\s*|\s+|$)(.*)$")
_ANNEX_STATUS = re.compile(r"^\((normative|informative)\)$", re.IGNORECASE)
_TOC_TITLES = {"contents", "table of contents"}
_TOC_ENTRY = re.compile(r"\.{2,}\s*\d+\s*$")
_TOC_ENTRY_DENSITY_THRESHOLD = 0.3
_HEADING_FONT_SIZE_MARGIN = 0.5


def _is_successor(previous: tuple[int, ...], candidate: tuple[int, ...]) -> bool:
    if candidate == previous + (1,):
        return True
    if candidate == (ANNEX_KEY_BASE,) and previous[0] < ANNEX_KEY_BASE:
        return True  # the last numbered clause is followed by "Annex A"
    for depth in range(1, len(previous) + 1):
        ancestor = previous[:depth]
        sibling = ancestor[:-1] + (ancestor[-1] + 1,)
        if candidate == sibling:
            return True
    return False


def _document_body_font_size(pages: list[ReconstructedPage]) -> float:
    """The most common word font size across the document - a proxy for "body text size"."""
    sizes = [word.size for page in pages for line in page.lines for word in line.words if word.size > 0]
    return statistics.mode(sizes) if sizes else 0.0


def _line_font_size(line: Line) -> float:
    sizes = [word.size for word in line.words if word.size > 0]
    return max(sizes) if sizes else 0.0


def _is_heading_sized(line: Line, body_font_size: float) -> bool:
    """Whether `line` is set in a visibly larger font than body text (always true without font data)."""
    if body_font_size <= 0:
        return True  # no font-size data available - regex only
    return _line_font_size(line) > body_font_size + _HEADING_FONT_SIZE_MARGIN


def _match_heading(line: Line, body_font_size: float) -> tuple[str, str] | None:
    """The (number, title) of a heading-shaped `line` - a clause number prefix or an "Annex X" heading,
    in a heading-sized font - or `None`. An annex heading whose title is on the next line has an
    empty title here."""
    if not _is_heading_sized(line, body_font_size):
        return None
    text = line.text.strip()
    annex = _ANNEX_HEADING.match(text)
    if annex:
        return annex.group(1), annex.group(2).strip()
    match = _HEADING.match(text)
    return (match.group(1), match.group(2).strip()) if match else None


def _flat_lines(pages: list[ReconstructedPage]) -> list[tuple[int, Line]]:
    return [(page.number, line) for page in pages for line in page.lines]


def _is_toc_entry(line: Line) -> bool:
    """Whether `line` has a Table of Contents entry's structural shape: "... title ..... NN"."""
    return bool(_TOC_ENTRY.search(line.text.strip()))


def _toc_entry_density(page: ReconstructedPage) -> float:
    lines = [line for line in page.lines if line.text.strip()]
    if not lines:
        return 0.0
    return sum(1 for line in lines if _is_toc_entry(line)) / len(lines)


def _find_toc_start_page(pages: list[ReconstructedPage]) -> int | None:
    """Return the index (into `pages`) of the page whose title is "Contents", if any."""
    for index, page in enumerate(pages):
        if any(line.text.strip().lower() in _TOC_TITLES for line in page.lines):
            return index
    return None


def _find_body_start(pages: list[ReconstructedPage]) -> int:
    """Return the flat-line index where the real body starts, skipping any Table of Contents.

    Scans pages after the "Contents" title page for the dotted-leader-entry density to drop -
    this is a structural signature of a TOC page, robust to however many pages it spans and however
    irregularly it numbers annexes, unlike matching the entry text against the heading pattern.
    """
    toc_page_index = _find_toc_start_page(pages)
    if toc_page_index is None:
        return 0

    body_page_index = next(
        (
            index
            for index in range(toc_page_index + 1, len(pages))
            if _toc_entry_density(pages[index]) < _TOC_ENTRY_DENSITY_THRESHOLD
        ),
        len(pages),
    )

    return sum(len(page.lines) for page in pages[:body_page_index])


def detect_clauses(pages: list[ReconstructedPage]) -> list[Clause]:
    """Walk reading-ordered pages and split them into numbered clauses."""
    body_font_size = _document_body_font_size(pages)
    flat_lines = _flat_lines(pages)
    start = _find_body_start(pages)

    clauses: list[Clause] = []
    current: Clause | None = None
    last_number: tuple[int, ...] | None = None
    annex_status: str | None = None

    for page_number, line in flat_lines[start:]:
        text = line.text.strip()
        heading = _match_heading(line, body_font_size)
        candidate_number = clause_number_key(heading[0]) if heading else None

        is_heading = heading is not None and (last_number is None or _is_successor(last_number, candidate_number))

        if is_heading:
            if current is not None:
                clauses.append(current)
            number, title = heading
            if number.isalpha():
                annex_status = None  # a new annex: its designation follows its title
            current = Clause(
                number=number,
                title=title,
                page_start=page_number,
                page_end=page_number,
                annex_status=annex_status,
            )
            last_number = candidate_number
        elif (
            current is not None and current.is_annex and not current.raw_lines and (status := _ANNEX_STATUS.match(text))
        ):
            annex_status = status.group(1).lower()
            current.annex_status = annex_status
            current.page_end = page_number
        elif current is not None and current.is_annex and not current.raw_lines and not current.title:
            # "Annex B" alone on its line: the title is the next line
            current.title = text
            current.page_end = page_number
        elif current is not None:
            current.raw_lines.append(line)
            current.page_end = page_number

    if current is not None:
        clauses.append(current)

    return clauses

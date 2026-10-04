"""Data model shared across the extraction pipeline stages."""

from __future__ import annotations

from dataclasses import dataclass, field

ANNEX_KEY_BASE = 1000
"""Offset an annex letter is mapped to in a clause number key, so that "Annex A" sorts after every
numeric clause and the annexes keep their letter order ("A" -> 1000, "B" -> 1001, ...)."""


def clause_number_key(number: str) -> tuple[int, ...]:
    """A sortable tuple for a clause number: "9.3.2" -> (9, 3, 2), "B.5.2" -> (1001, 5, 2)."""
    head, *rest = number.split(".")
    first = ANNEX_KEY_BASE + ord(head) - ord("A") if head.isalpha() else int(head)
    return (first, *(int(part) for part in rest))


@dataclass(frozen=True)
class PositionedWord:
    """A single word with its bounding box and font, as reported by pdfplumber."""

    text: str
    x0: float
    x1: float
    top: float
    bottom: float
    font_name: str
    size: float


@dataclass(frozen=True)
class PositionedPage:
    """The raw positioned words on one page, in no particular order."""

    number: int
    height: float
    words: tuple[PositionedWord, ...]


@dataclass(frozen=True)
class Line:
    """One reading-order line: words sorted left-to-right at a shared height."""

    top: float
    words: tuple[PositionedWord, ...]

    @property
    def text(self) -> str:
        return " ".join(word.text for word in self.words)


@dataclass(frozen=True)
class ReconstructedPage:
    """A page's lines in top-to-bottom reading order, headers/footers stripped."""

    number: int
    lines: tuple[Line, ...]


@dataclass
class Paragraph:
    """One paragraph of a clause's body text."""

    text: str
    is_normative: bool = False
    informative_kind: str | None = None  # "note" | "example" | None


@dataclass
class Clause:
    """One detected clause of the specification: a numbered heading plus its body.

    `number` is either dotted-decimal ("9.3.2") or, inside an annex, letter-prefixed ("B" for the
    annex itself, "B.5.2" for its subclauses). `raw_lines` holds the body as reading-ordered lines,
    populated by clause detection; `paragraphs` is populated afterwards by the normative/informative
    split. `annex_status` is the annex's own "(normative)"/"(informative)" designation, set on an
    annex and all of its subclauses, and `None` for the numbered body clauses.
    """

    number: str
    title: str
    page_start: int
    page_end: int
    raw_lines: list[Line] = field(default_factory=list)
    paragraphs: list[Paragraph] = field(default_factory=list)
    annex_status: str | None = None  # "normative" | "informative" | None

    @property
    def is_annex(self) -> bool:
        """Whether this is an annex itself ("B"), as opposed to one of its subclauses ("B.1")."""
        return self.number.isalpha()

    @property
    def heading_label(self) -> str:
        """The clause number as the specification prints it in the heading: "9.3.2", "Annex B", "B.1"."""
        return f"Annex {self.number}" if self.is_annex else self.number

    @property
    def is_normative(self) -> bool:
        if self.annex_status is not None:
            return self.annex_status == "normative"
        return any(paragraph.is_normative for paragraph in self.paragraphs)

    @property
    def slug(self) -> str:
        slug = "".join(character if character.isalnum() else "-" for character in self.title.lower())
        while "--" in slug:
            slug = slug.replace("--", "-")
        return slug.strip("-")

    @property
    def file_name(self) -> str:
        return f"{self.number}-{self.slug}.md"

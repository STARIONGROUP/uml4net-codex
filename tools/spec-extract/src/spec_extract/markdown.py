"""Stage 5: render one clause to a markdown file with YAML front matter."""

from __future__ import annotations

from spec_extract.models import Clause


def _yaml_escape(text: str) -> str:
    return text.replace('"', '\\"')


def render_clause_markdown(clause: Clause, *, document: str, version: str) -> str:
    """Render a single clause as markdown: YAML front matter, heading, then body."""
    pages = f"{clause.page_start}" if clause.page_start == clause.page_end else f"{clause.page_start}-{clause.page_end}"

    front_matter = "\n".join(
        [
            "---",
            f'clause: "{clause.number}"',
            f'title: "{_yaml_escape(clause.title)}"',
            f'document: "{document}"',
            f'version: "{version}"',
            f'pages: "{pages}"',
            f"normative: {'true' if clause.is_normative else 'false'}",
            "---",
        ]
    )

    body_parts = [f"# {clause.heading_label} {clause.title}"]
    for paragraph in clause.paragraphs:
        if paragraph.informative_kind:
            body_parts.append(f"<!-- informative:{paragraph.informative_kind} -->")
            body_parts.append(paragraph.text)
            body_parts.append(f"<!-- /informative:{paragraph.informative_kind} -->")
        else:
            body_parts.append(paragraph.text)

    return front_matter + "\n\n" + "\n\n".join(body_parts) + "\n"

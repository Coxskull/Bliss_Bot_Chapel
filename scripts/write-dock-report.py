#!/usr/bin/env python3
"""Write one letter-size dock report from a UTF-8 text file."""

import sys
from pathlib import Path

from fpdf import FPDF


def main() -> None:
    if len(sys.argv) != 3:
        print("usage: write-dock-report.py INPUT.txt OUTPUT.pdf", file=sys.stderr)
        sys.exit(2)
    source = Path(sys.argv[1]).read_text(encoding="utf-8")
    output = Path(sys.argv[2])
    pdf = FPDF(format="Letter")
    pdf.set_auto_page_break(auto=True, margin=18)
    pdf.set_margins(18, 18, 18)
    pdf.add_page()
    pdf.set_font("Helvetica", size=11)
    for paragraph in source.split("\n"):
        text = paragraph.encode("latin-1", "replace").decode("latin-1")
        if text.strip() == "":
            pdf.ln(4)
            continue
        pdf.multi_cell(0, 6, text)
    output.parent.mkdir(parents=True, exist_ok=True)
    pdf.output(str(output))


if __name__ == "__main__":
    main()

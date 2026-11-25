r"""Render the release README (APE README source/readme.html) to a versioned PDF.

Needs WeasyPrint plus its native Pango libs. On Windows: `pip install weasyprint`,
then install MSYS2 and `pacman -S mingw-w64-x86_64-pango`. The libs' folder is
found via WEASYPRINT_DLL_DIRECTORIES, defaulting to C:\msys64\mingw64\bin.

    python tools/build_readme_pdf.py        -> APE README source/APE Patch Extensions_v<ver>.pdf

The version comes from src/patcher_gui/Version.cs, the same single source the exe uses.
"""
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "APE README source"

os.environ.setdefault("WEASYPRINT_DLL_DIRECTORIES", r"C:\msys64\mingw64\bin")
from weasyprint import HTML  # noqa: E402

version = re.search(r'const string Current\s*=\s*"([^"]+)"',
                    (ROOT / "src/patcher_gui/Version.cs").read_text(encoding="utf-8")).group(1)
html = (SRC / "readme.html").read_text(encoding="utf-8")
if f">v{version}<" not in html:
    sys.exit(f"readme.html title page does not say v{version}; update it first")

out = SRC / f"APE Patch Extensions_v{version}.pdf"
HTML(filename=str(SRC / "readme.html")).write_pdf(str(out))
print("wrote", out)

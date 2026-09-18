# Regenerating the Greek manuals

The application build does not depend on the documentation tools.

`build_manuals.py` uses Python with `python-docx`, the unchanged references in
`tools/document_templates`, the final Markdown in `Documentation`, and runtime
screenshots in `Evidence/Runtime`. Run it from any directory to write the two
editable manuals to `output/documents`.

For Windows layout checks, `export_manuals_word.ps1` requires installed Microsoft
Word and an interactive Windows account with Word COM access. It opens documents
read-only and exports temporary PDFs to `tmp/docs/word-pdf`; it does not modify
the DOCX files. The QA renderer then uses the packaged Codex document renderer
and bundled Poppler. Pass explicit `--renderer` and `--dependencies` paths when
automatic discovery is unavailable. It uses the Word-produced PDF for layout
and the canonical renderer for page sizing and PNG generation. No desktop
LibreOffice installation is used.

When rebuilding final documents, verify that every referenced screenshot exists,
export a fresh PDF from the new DOCX, render every page, and inspect all pages for
clipping, overflow and readability. Temporary PDF and PNG output is QA material;
the final manuals are the editable DOCX files and the matching verified PDFs in
`output/documents`. The page PNG files remain internal QA material.

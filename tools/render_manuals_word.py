"""Render DOCX using the canonical document renderer and a Word-produced PDF.

The Windows workspace bundle has no LibreOffice. Microsoft Word supplies the
layout PDF; the unmodified skill renderer supplies page sizing and raster QA.
"""
import argparse
import importlib.util
import os
import shutil
import sys
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('docx')
p.add_argument('pdf')
p.add_argument('out')
p.add_argument('--renderer', help='Path to the document skill render_docx.py')
p.add_argument('--dependencies', help='Codex workspace dependencies directory')
a = p.parse_args()
root = Path(a.dependencies or os.environ.get('CODEX_WORKSPACE_DEPENDENCIES') or Path.home()/'.cache/codex-runtimes/codex-primary-runtime/dependencies')
if not (root/'native/poppler/Library/bin').is_dir():
    raise SystemExit('Bundled Poppler was not found. Supply --dependencies from the workspace dependency loader.')
os.environ['PATH'] = str(root/'native/poppler/Library/bin') + os.pathsep + os.environ['PATH']
skill_arg = a.renderer or os.environ.get('CODEX_DOCUMENT_RENDERER')
if skill_arg:
    skill = Path(skill_arg)
else:
    matches = list((Path.home()/'.codex/plugins/cache/openai-primary-runtime/documents').glob('*/skills/documents/render_docx.py'))
    if not matches:
        raise SystemExit('Document renderer not found. Supply --renderer with the document skill render_docx.py path.')
    skill = max(matches, key=lambda x:x.stat().st_mtime)
if not skill.is_file(): raise SystemExit('Document renderer does not exist: '+str(skill))
spec = importlib.util.spec_from_file_location('render_docx', skill)
renderer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(renderer)
def use_word_pdf(input_path, user_profile, convert_tmp_dir, stem, verbose=False):
    target = Path(convert_tmp_dir)/(stem+'.pdf')
    shutil.copy2(a.pdf, target)
    return str(target), 'Microsoft Word 16 native PDF export; bundled LibreOffice unavailable on Windows.'
renderer.convert_to_pdf = use_word_pdf
sys.argv = [str(skill), a.docx, '--output_dir', a.out, '--emit_pdf']
renderer.main()

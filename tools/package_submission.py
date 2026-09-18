"""Package verified outputs; never rebuild or modify the Application folder."""
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import re
import shutil
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / 'output' / 'ICU_Submission'
STAGING = ('Source', 'Scenarios', 'Documentation', 'Evidence')
MANUALS = ('ICU_User_Manual_EL.docx', 'ICU_Technical_Manual_HTA_EL.docx')
EVIDENCE = ('EditMode-final.xml', 'WindowsBuild.txt', 'verification-summary.md', 'documentation-qa.json')
SCREENSHOTS = ('01_scenario_menu', '02_icu_overview', '03_monitor_alarm', '04_ehr_validation',
               '05_ehr_saved', '06_debrief', '07_help', '08_second_scenario')


def require(path):
    if not path.is_file() or path.stat().st_size == 0:
        raise SystemExit('Required release file is missing or empty: ' + str(path))
    return path


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def utc(value):
    # Unity/.NET writes seven fractional digits; Python 3.10 accepts at most six.
    value = re.sub(r'(\.\d{6})\d+', r'\1', value)
    timestamp = datetime.fromisoformat(value.replace('Z', '+00:00'))
    if timestamp.tzinfo is None:
        raise SystemExit('Verification timestamps must include a timezone: ' + value)
    return timestamp.astimezone(timezone.utc)


def ship(path):
    for part in path.parts:
        compact = part.lower().replace('_', '').replace('-', '').replace(' ', '')
        if part == '__pycache__' or compact.startswith(('performancetestrun', 'inittestscene')):
            return False
        if 'donotship' in compact or 'dontship' in compact or 'generatedbuildsymbols' in compact:
            return False
    return path.suffix.lower() not in ('.pyc', '.pyo')


def reset_staging():
    """Validate all four absolute targets before deleting any previous staged output."""
    expected = ROOT.resolve() / 'output' / 'ICU_Submission'
    if PACKAGE.resolve() != expected or not PACKAGE.is_dir():
        raise SystemExit('Unexpected submission directory; refusing staging reset.')
    targets = [PACKAGE / name for name in STAGING]
    for target in targets:
        if target.resolve() != expected / target.name or target.is_symlink():
            raise SystemExit('Staging path redirects outside its expected location: ' + str(target))
        if target.exists() and not target.is_dir():
            raise SystemExit('Staging target is not a directory: ' + str(target))
    # Application is never a target; only the exact four validated folders are reset.
    for target in targets:
        if target.exists():
            shutil.rmtree(target)
        target.mkdir()


def main():
    # Runtime is the immutable capture cited by the delivered manuals. FinalRuntime verifies the final player.
    manual_runtime = ROOT / 'Evidence' / 'Runtime'
    runtime = ROOT / 'Evidence' / 'FinalRuntime'
    results = json.loads(require(runtime / 'acceptance-results.json').read_text(encoding='utf-8-sig'))
    if results.get('passed') is not True or results.get('errors') or len(results.get('checks', [])) < 38 or results.get('platform') != 'WindowsPlayer':
        raise SystemExit('FinalRuntime standalone Windows acceptance must pass at least 38 checks without errors.')
    manual_results = json.loads(require(manual_runtime / 'acceptance-results.json').read_text(encoding='utf-8-sig'))
    if manual_results.get('passed') is not True or manual_results.get('errors'):
        raise SystemExit('Preserve the passed Runtime dataset cited by the manuals.')
    tests = ET.parse(require(ROOT / 'Evidence' / 'EditMode-final.xml')).getroot()
    if tests.attrib.get('result') != 'Passed' or int(tests.attrib.get('failed', '0')) or int(tests.attrib.get('total', '0')) == 0:
        raise SystemExit('Unity EditMode tests have not passed.')
    report = require(ROOT / 'Evidence' / 'WindowsBuild.txt').read_text(encoding='utf-8-sig')
    build = dict(line.split(': ', 1) for line in report.splitlines() if ': ' in line)
    if build.get('Result') != 'Succeeded' or build.get('Errors') != '0' or build.get('Development build') != 'false':
        raise SystemExit('A successful non-development Windows build with zero errors is required.')
    if build.get('Scene') != 'Assets/Scenes/ICUTraining.unity':
        raise SystemExit('Build report does not identify the final ICUTraining scene.')
    if not build.get('Built UTC') or not results.get('completedUtc') or utc(results['completedUtc']) < utc(build['Built UTC']):
        raise SystemExit('Run standalone acceptance after building the final player.')
    application = PACKAGE / 'Application'
    for name in ('ICU-XR-Simulation.exe', 'UnityPlayer.dll', 'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll',
                 'ICU-XR-Simulation_Data/boot.config', 'ICU-XR-Simulation_Data/globalgamemanagers',
                 'ICU-XR-Simulation_Data/level0', 'ICU-XR-Simulation_Data/resources.assets',
                 'ICU-XR-Simulation_Data/Managed/Assembly-CSharp.dll',
                 'ICU-XR-Simulation_Data/Managed/ICUSimulation.Scenarios.dll'):
        require(application / name)
    temporary = [p for p in (ROOT / 'Assets/Editor').rglob('*.cs') if p.name != 'ReleaseBuild.cs']
    if temporary:
        raise SystemExit('Remove temporary Editor utilities: ' + ', '.join(str(p.relative_to(ROOT)) for p in temporary))
    manuals = [require(ROOT / 'output/documents' / name) for name in MANUALS]
    for manual in manuals:
        if not zipfile.is_zipfile(manual):
            raise SystemExit('Invalid final DOCX: ' + str(manual))
        with zipfile.ZipFile(manual) as archive:
            if 'word/document.xml' not in archive.namelist() or archive.testzip() is not None:
                raise SystemExit('Incomplete or damaged final DOCX: ' + str(manual))
    pdfs = [require(ROOT / 'output/documents' / Path(name).with_suffix('.pdf')) for name in MANUALS]
    if any(not path.read_bytes().startswith(b'%PDF-') for path in pdfs):
        raise SystemExit('Both rendered PDF manuals are required.')
    documentation_qa = json.loads(require(ROOT / 'Evidence/documentation-qa.json').read_text(encoding='utf-8-sig'))
    reviewed_files = {item['file']: item['sha256'] for item in documentation_qa.get('files', [])}
    for path in manuals + pdfs:
        if reviewed_files.get(path.relative_to(ROOT).as_posix()) != sha256(path):
            raise SystemExit('Final manual differs from its documented QA result: ' + path.name)
    scenarios = sorted((ROOT / 'Assets/StreamingAssets/Scenarios').glob('*.json'))
    if not scenarios:
        raise SystemExit('No scenario JSON files were found.')
    for scenario in scenarios:
        player_scenario = require(application / 'ICU-XR-Simulation_Data/StreamingAssets/Scenarios' / scenario.name)
        if sha256(scenario) != sha256(player_scenario):
            raise SystemExit('Player scenario differs from source; rebuild: ' + scenario.name)
    for name in EVIDENCE:
        require(ROOT / 'Evidence' / name)
    for dataset in (manual_runtime, runtime):
        for name in SCREENSHOTS:
            require(dataset / (name + '.png'))
        require(dataset / 'completed-session.json')
    session = json.loads(require(runtime / 'completed-session.json').read_text(encoding='utf-8-sig'))
    if session.get('completed') is not True or session.get('debrief', {}).get('UsedDebugBypass') is not False:
        raise SystemExit('A completed session without debug gate bypass is required.')
    runtime_files = sorted(p for dataset in (manual_runtime, runtime) for p in dataset.glob('*')
                           if p.is_file() and p.suffix in ('.json', '.png'))
    sources = []
    for directory in ('Assets', 'Packages', 'ProjectSettings', 'Documentation'):
        sources.extend(p for p in (ROOT / directory).rglob('*') if p.is_file() and ship(p.relative_to(ROOT)))
    # The documentation helpers now use relative/configurable paths and bundled draft references.
    for name in ('package_submission.py', 'build_manuals.py', 'export_manuals_word.ps1', 'render_manuals_word.py', 'README_documentation.md'):
        sources.append(require(ROOT / 'tools' / name))
    sources.extend(p for p in (ROOT / 'tools/document_templates').rglob('*') if p.is_file() and ship(p.relative_to(ROOT)))
    sources += [require(ROOT / name) for name in ('README.md', '.gitignore', '.gitattributes')]
    sources += runtime_files + [ROOT / 'Evidence' / name for name in EVIDENCE]
    sources = sorted(set(sources))
    app_files = sorted(p for p in application.rglob('*') if p.is_file() and ship(p.relative_to(application)))

    # All validation above finishes before replacing any previous staging output.
    reset_staging()
    for path in manuals + pdfs:
        shutil.copy2(path, PACKAGE / 'Documentation' / path.name)
    for path in scenarios:
        shutil.copy2(path, PACKAGE / 'Scenarios' / path.name)
    for path in (ROOT / 'Documentation').glob('*.md'):
        shutil.copy2(path, PACKAGE / 'Documentation' / path.name)
    for name in EVIDENCE:
        shutil.copy2(ROOT / 'Evidence' / name, PACKAGE / 'Evidence' / name)
    for path in runtime_files:
        destination = PACKAGE / path.relative_to(ROOT)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, destination)
    manifest = {p.relative_to(ROOT).as_posix(): sha256(p) for p in sources}
    (PACKAGE / 'Source/source-manifest.json').write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding='utf-8')
    with zipfile.ZipFile(PACKAGE / 'Source/ICU-XR-Simulation_Source.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sources:
            archive.write(path, path.relative_to(ROOT))
    app_manifest = {'algorithm': 'SHA256', 'build_utc': build['Built UTC'], 'acceptance_completed_utc': results['completedUtc'],
                    'acceptance_evidence': 'Evidence/FinalRuntime/acceptance-results.json',
                    'documentation_capture_utc': manual_results['completedUtc'],
                    'note': 'Hashes identify packaged player files; acceptance timestamp is checked against the successful build.',
                    'files': {p.relative_to(application).as_posix(): sha256(p) for p in app_files}}
    (PACKAGE / 'Evidence/application-manifest.json').write_text(json.dumps(app_manifest, indent=2, ensure_ascii=False), encoding='utf-8')
    readme = '''ΠΑΡΑΔΟΣΗ ΠΡΟΣΟΜΟΙΩΣΗΣ ΜΕΘ

Εκτέλεση: Application/ICU-XR-Simulation.exe σε Windows 64 bit.
Αποσυμπιέστε ολόκληρο το πακέτο. Διατηρήστε όλα τα αρχεία του Application μαζί.
Scenarios → επιλογή → Load selected → Start training.
W A S D: κίνηση, ποντίκι: θέαση, E: αλληλεπίδραση, Enter/C: συνέχεια.
F1: βοήθεια, F2: μενού, Escape: κλείσιμο παραθύρου.

Application: πλήρες εκτελέσιμο.
Source: ZIP πηγαίου κώδικα και SHA256 αρχείων. Unity 6000.4.8f1.
Ανοίξτε τη σκηνή Assets/Scenes/ICUTraining.unity μετά την εισαγωγή στο Unity.
Scenarios: αρχικό JSON και δεύτερη παραλλαγή εξάσκησης.
Documentation: ελληνικά εγχειρίδια χρήστη και τεχνικής ανάλυσης με HTA.
Evidence: πραγματικά αποτελέσματα ελέγχων, screenshots και εξαγωγή συνεδρίας.
Evidence/Runtime: διατηρημένο σύνολο εικόνων και ελέγχων της 18/09/2026, 13:06:48 UTC, στο οποίο παραπέμπουν τα εγχειρίδια.
Evidence/FinalRuntime: νέος έλεγχος του τελικού εκτελέσιμου μετά τις τελευταίες οπτικές διορθώσεις.
Evidence/documentation-qa.json: οπτικός έλεγχος, στοιχεία προσβασιμότητας και SHA256 των δύο DOCX και δύο PDF.
Evidence/application-manifest.json: SHA256 των αρχείων του εκτελέσιμου στο πακέτο.

Τα ονόματα και οι αριθμοί μητρώου παραλείφθηκαν κατόπιν αιτήματος.
Διαβάστε το Evidence/verification-summary.md για τα όρια των ελέγχων και το μητρώο assets για την προέλευσή τους.
'''
    (PACKAGE / 'README.txt').write_text(readme, encoding='utf-8-sig')
    outer_files = app_files + [PACKAGE / 'README.txt']
    for folder in STAGING:
        outer_files.extend(p for p in (PACKAGE / folder).rglob('*') if p.is_file() and ship(p.relative_to(PACKAGE)))
    outer = ROOT / 'output/ICU_Submission.zip'
    temporary_archive = outer.with_suffix('.zip.tmp')
    with zipfile.ZipFile(temporary_archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(outer_files):
            archive.write(path, Path('ICU_Submission') / path.relative_to(PACKAGE))
    temporary_archive.replace(outer)
    (ROOT / 'output/ICU_Submission.sha256').write_text(sha256(outer) + '  ICU_Submission.zip\n', encoding='ascii')
    print(json.dumps({'archive': str(outer), 'bytes': outer.stat().st_size, 'source_files': len(sources),
                      'application_files': len(app_files), 'checks': len(results['checks']), 'unity_tests': tests.attrib['total']}))


if __name__ == '__main__':
    main()

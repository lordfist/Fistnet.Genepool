"""Read-only build evidence and binary identity checks shared by verification runners."""
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
KB = ROOT / "KnowledgeBase"


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def report_path(value):
    path = (KB / value).resolve()
    if path.parent != KB.resolve() or path.suffix.lower() != ".json":
        raise ValueError("Report must be a JSON file directly inside KnowledgeBase")
    return path


def project_target(project):
    path = ROOT / project / (project + ".csproj")
    targets = [element.text.strip() for element in ET.parse(path).iter()
               if element.tag.rsplit("}", 1)[-1] == "TargetFramework" and element.text]
    if len(targets) != 1 or any(character in targets[0] for character in "/\\:$"):
        raise ValueError(f"Expected one literal TargetFramework in {path}")
    return targets[0]


def project_output(project, configuration):
    target = project_target(project)
    if project == "Fistnet.Genepool.Godot":
        return ROOT / project / ".godot" / "mono" / "temp" / "bin" / configuration
    return ROOT / project / "bin" / configuration / target


def rooted_path(relative):
    path = (ROOT / relative).resolve()
    if not path.is_relative_to(ROOT.resolve()) or path == ROOT.resolve():
        raise ValueError("Build evidence contains a path outside the project")
    return path


def capture_outputs(directory, assemblies):
    return {name: digest(directory / (name + ".dll")) for name in assemblies}


def verify_loaded(expected, actual):
    if not isinstance(actual, dict):
        raise ValueError("Verification result is missing loaded assembly identities")
    mismatched = [name for name, sha in expected.items()
                  if str(actual.get(name, "")).lower() != sha.lower()]
    if mismatched:
        raise ValueError("Loaded assembly identity mismatch: " + ", ".join(mismatched))


def verify_build(report, stage, configuration, required_outputs):
    """Require an exact successful build stage whose sources and outputs still match."""
    if not report or not stage:
        raise ValueError("A build report and exact build stage are required")
    path = report_path(report)
    raw = path.read_bytes()
    data = json.loads(raw)
    build = data.get("stages", {}).get(stage)
    if not isinstance(build, dict) or build.get("ok") is not True:
        raise ValueError("Selected build stage is absent or did not complete successfully")
    if build.get("configuration") != configuration:
        raise ValueError("Selected build stage has the wrong solution configuration")
    sources = build.get("source_sha256_after", build.get("source_sha256"))
    outputs = build.get("output_sha256")
    if not isinstance(sources, dict) or not sources or not isinstance(outputs, dict) or not outputs:
        raise ValueError("Selected build stage has no source/output identity evidence")
    for relative, sha in sources.items():
        if digest(rooted_path(relative)).lower() != str(sha).lower():
            raise ValueError("Source changed after selected build: " + relative)
    for output in required_outputs:
        relative = output.relative_to(ROOT).as_posix()
        expected = outputs.get(relative)
        if not expected or digest(output).lower() != str(expected).lower():
            raise ValueError("Output differs from selected build or is unrecorded: " + relative)
    return {"report": str(path), "stage": stage, "configuration": configuration,
            "report_sha256": hashlib.sha256(raw).hexdigest(), "sources_verified": len(sources),
            "outputs_verified": len(required_outputs)}

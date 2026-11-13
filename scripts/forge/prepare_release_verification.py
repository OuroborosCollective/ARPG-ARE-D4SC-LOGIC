"""Stage release-only Unity verification sources in an isolated imported Assets tree."""
import hashlib
import json
from pathlib import Path
import shutil
import sys


def prepare(project):
    repository = Path(__file__).resolve().parents[2]
    target = Path(project).resolve()
    if target == repository or repository in target.parents:
        raise ValueError("Use an isolated verification project outside the game checkout")
    if not (target / "Assets/AshenCrypt/MemoryForge").is_dir():
        raise ValueError("Import the Memory Forge package into the verification project first")
    source = repository / "Tools/ForgeReleaseVerification"
    destination = target / "Assets/ForgeReleaseVerification"
    files = sorted(p for p in source.rglob("*") if p.is_file() and p.suffix in (".cs", ".asmdef"))
    for path in files:
        copy = destination / path.relative_to(source)
        if copy.exists() and copy.read_bytes() != path.read_bytes():
            raise ValueError("Existing verification source differs: " + str(copy))
    receipt = []
    for path in files:
        copy = destination / path.relative_to(source)
        copy.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, copy)
        receipt.append({"path": str(copy.relative_to(target)), "sha256": hashlib.sha256(copy.read_bytes()).hexdigest()})
    return {"destination": str(destination), "files": receipt}


if __name__ == "__main__":
    print(json.dumps(prepare(sys.argv[1]), indent=2))

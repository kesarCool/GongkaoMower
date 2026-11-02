"""Install the pinned Unity package from the already downloaded source archive."""
import hashlib
import json
from pathlib import Path
import tarfile

root = Path(__file__).resolve().parents[2]
installation = json.loads((Path(__file__).parent / "installation.json").read_text(encoding="utf-8-sig"))
source = Path(installation["source"]) / "MCPForUnity"
version = installation["packageVersion"]
package_dir = Path(__file__).parent / "Packages"
package_dir.mkdir(exist_ok=True)
archive = package_dir / f"com.coplaydev.unity-mcp-{version}.tgz"
if not archive.exists():
    with tarfile.open(archive, "w:gz") as bundle:
        bundle.add(source, arcname="package")

manifest_path = root / "Packages" / "manifest.json"
manifest_text = manifest_path.read_text(encoding="utf-8-sig")
manifest = json.loads(manifest_text)
backup = Path(__file__).parent / "manifest.before-unity-mcp.json"
if not backup.exists():
    backup.write_text(manifest_text, encoding="utf-8")
manifest["dependencies"]["com.coplaydev.unity-mcp"] = f"file:../Tools/UnityMCP/Packages/{archive.name}"
manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(json.dumps({"archive": str(archive), "bytes": archive.stat().st_size,
                  "sha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
                  "unity_package": version}, indent=2))

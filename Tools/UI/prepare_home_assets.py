"""Copy approved UI exports into English-only asset paths without replacing existing assets."""
import csv
import hashlib
import json
from pathlib import Path
import shutil
import sys

root = Path(__file__).resolve().parents[2]
phase = sys.argv[1] if len(sys.argv) > 1 else "home"
delivery = root / "美术资源/UI交付" / ("文字割草机_主界面美食篇8关_v1_定稿交付" if phase == "home" else "角色面板_v5_定稿交付")
csv_path = delivery / "03_适配说明/Unity导入参数.csv"
records = []
for row in csv.DictReader(csv_path.open(encoding="utf-8-sig")):
    source_name = row.get("filename", row.get("file", row.get("path", "")))
    if not source_name:
        continue
    source = delivery / "01_散图" / source_name
    if not source.exists():
        matches = list((delivery / "01_散图").rglob(Path(source_name).name))
        if len(matches) != 1:
            raise RuntimeError(f"Cannot resolve source: {source_name}")
        source = matches[0]
    name = source.name
    shared = phase == "home" and any(group in source_name for group in ["基础UI/", "按钮/", "图标/", "底部导航/", "关卡列表/"])
    shared = shared or "公共UI" in source_name or name == "top_safe_gradient_strip.png"
    folder = "Assets/Res/image/common" if shared else "Assets/Res/image/Home/" + ("roadmap" if phase == "home" else "character")
    destination = root / folder / name
    destination.parent.mkdir(parents=True, exist_ok=True)
    old_nav = root / "Assets/Res/image/Home" / name
    move_from = old_nav.relative_to(root).as_posix() if shared and old_nav.exists() else ""
    if not destination.exists() and not move_from:
        shutil.copy2(source, destination)
    if destination.exists() and hashlib.sha256(destination.read_bytes()).digest() != hashlib.sha256(source.read_bytes()).digest():
        raise RuntimeError(f"Existing asset differs; not overwritten: {destination}")
    def number(*keys, default=0):
        return int(next((row[k] for k in keys if row.get(k)), default))
    records.append({"path": destination.relative_to(root).as_posix(), "moveFrom": move_from,
                    "border": [number("borderLeft", "border_left"), number("borderBottom", "border_bottom"), number("borderRight", "border_right"), number("borderTop", "border_top")],
                    "maxSize": number("maxTextureSize", "max_size", default=256)})
path = root / f"Tools/UI/{phase}-assets.json"
path.write_text(json.dumps({"assets": records}, indent=2), encoding="utf-8")
print(json.dumps({"phase": phase, "assets": len(records), "manifest": str(path)}, indent=2))


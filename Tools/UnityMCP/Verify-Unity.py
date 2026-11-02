"""Read-only installation check against this project's connected Unity editor."""
import asyncio
import json
from datetime import datetime, timezone
from pathlib import Path
from fastmcp import Client

ROOT = Path(__file__).resolve().parents[2]


def text_data(contents):
    items = []
    for content in contents:
        if hasattr(content, "text"):
            try:
                items.append(json.loads(content.text))
            except json.JSONDecodeError:
                items.append(content.text)
    return items[0] if len(items) == 1 else items


async def main():
    report = {"utc": datetime.now(timezone.utc).isoformat()}
    async with Client("http://127.0.0.1:8767/mcp", timeout=45) as client:
        for name, uri in [
            ("instances", "mcpforunity://instances"),
            ("project_info", "mcpforunity://project/info"),
            ("editor_state", "mcpforunity://editor/state"),
        ]:
            report[name] = text_data(await client.read_resource(uri))
            print(json.dumps({name: report[name]}, ensure_ascii=False), flush=True)
        checks = [
            ("active_scene", "manage_scene", {"action": "get_active"}),
            ("hierarchy", "manage_scene", {"action": "get_hierarchy", "max_depth": 2, "max_nodes": 30, "include_transform": True}),
            ("card_selection_prefab", "manage_prefabs", {"action": "get_info", "prefab_path": "Assets/Prefab/CardSelect/CardSelectionPanel.prefab"}),
            ("card_view_prefab", "manage_prefabs", {"action": "get_hierarchy", "prefab_path": "Assets/Prefab/CardSelect/CardView.prefab"}),
            ("console_errors", "read_console", {"action": "get", "types": ["error"], "count": "10", "format": "json"}),
            ("screenshot", "manage_camera", {"action": "screenshot", "capture_source": "game_view", "screenshot_file_name": "installation-game-view", "include_image": False, "output_folder": "Library/UnityMCPTools/verification"}),
        ]
        for label, tool, arguments in checks:
            response = await client.call_tool(tool, arguments, raise_on_error=False)
            report[label] = text_data(response.content)
            preview = json.dumps({label: report[label]}, ensure_ascii=False)
            print(preview[:1700], flush=True)
    path = ROOT / "Library/UnityMCPTools/verification.json"
    path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print("Full report: " + str(path))


asyncio.run(main())

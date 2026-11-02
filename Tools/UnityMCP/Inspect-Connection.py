"""Read-only MCP discovery. Never changes Unity scenes or assets."""
import asyncio
import json
from pathlib import Path
from fastmcp import Client


async def main():
    async with Client("http://127.0.0.1:8767/mcp", timeout=30) as client:
        tools = await client.list_tools()
        resources = await client.list_resources()
        result = {
            "tool_count": len(tools),
            "tools": [t.model_dump(mode="json") for t in tools],
            "resources": [r.model_dump(mode="json") for r in resources],
        }
        instances = await client.read_resource("mcpforunity://instances")
        result["instances"] = [item.model_dump(mode="json") for item in instances]
        report = Path(__file__).resolve().parents[2] / "Library/UnityMCPTools/discovery.json"
        report.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
        print(json.dumps({"tool_count": len(tools), "tools": [t.name for t in tools],
                          "resources": [{"name": r.name, "uri": str(r.uri)} for r in resources],
                          "instances": result["instances"],
                          "report": str(report)}, indent=2, ensure_ascii=False))


asyncio.run(main())

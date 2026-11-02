"""Call the local Unity MCP using a JSON request file; preserve full results on disk."""
import argparse
import asyncio
import json
from pathlib import Path
from fastmcp import Client


async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("request")
    parser.add_argument("--output", default="Library/UnityMCPTools/last-call.json")
    args = parser.parse_args()
    request = json.loads(Path(args.request).read_text(encoding="utf-8-sig"))
    async with Client("http://127.0.0.1:8767/mcp", timeout=90) as client:
        if "resource" in request:
            contents = await client.read_resource(request["resource"])
        else:
            result = await client.call_tool(request["tool"], request.get("arguments", {}), raise_on_error=False)
            contents = result.content
        output = []
        for item in contents:
            if hasattr(item, "text"):
                try:
                    output.append(json.loads(item.text))
                except json.JSONDecodeError:
                    output.append(item.text)
        data = output[0] if len(output) == 1 else output
        Path(args.output).parent.mkdir(parents=True, exist_ok=True)
        encoded = json.dumps(data, indent=2, ensure_ascii=False)
        Path(args.output).write_text(encoded, encoding="utf-8")
        print(encoded[:18000])


asyncio.run(main())

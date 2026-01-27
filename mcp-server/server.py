from __future__ import annotations

import os
from datetime import datetime
from zoneinfo import ZoneInfo

from dotenv import load_dotenv
from mcp.server.fastmcp import FastMCP

# .env aus Repo-Root laden
load_dotenv(os.path.join(os.path.dirname(__file__), "..", ".env"))

mcp = FastMCP("adesso-gpt-demo")


@mcp.tool()
def add(a: int, b: int) -> int:
    """Addiert zwei ganze Zahlen"""
    return a + b


@mcp.tool()
def get_time(tz: str = "Europe/Berlin") -> str:
    """Gibt die aktuelle Zeit als ISO-String zurück"""
    return datetime.now(ZoneInfo(tz)).isoformat(timespec="seconds")


if __name__ == "__main__":
    mcp.run(transport="streamable-http")

"""Small, deployment-safe environment loader for local agent development.

Process environment variables are authoritative. The repository-local .env is
only a convenience for development and is never allowed to overwrite a value
provided by a host, container, or CI system.
"""

from __future__ import annotations

import os
from pathlib import Path


def load_dotenv_file(path: Path) -> None:
    """Load simple KEY=VALUE entries without replacing existing variables."""

    if not path.is_file():
        return

    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("export "):
            line = line[7:].lstrip()
        key, separator, value = line.partition("=")
        key = key.strip()
        if not separator or not key or not key.replace("_", "").isalnum():
            continue
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
            value = value[1:-1]
        os.environ.setdefault(key, value)


def load_local_environment() -> None:
    """Load the project .env regardless of the process working directory."""

    load_dotenv_file(Path(__file__).resolve().parent / ".env")


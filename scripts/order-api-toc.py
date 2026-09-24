#!/usr/bin/env python3

from pathlib import Path
import re
import sys


ORDER = {
    "Ahr.Foundation.Result": 0,
    "Ahr.Foundation.Result`1": 1,
    "Ahr.Foundation.Result`2": 2,
    "Ahr.Foundation.Option`1": 3,
    "Ahr.Foundation.Error": 4,
    "Ahr.Foundation.ResultExtensions": 5,
    "Ahr.Foundation.OptionExtensions": 6,
    "Ahr.Foundation.TaskCompositionExtensions": 7,
    "Ahr.Foundation.LinqExtensions": 8,
}


def main() -> int:
    toc_path = Path(sys.argv[1] if len(sys.argv) > 1 else "api/toc.yml")
    content = toc_path.read_text(encoding="utf-8")
    match = re.fullmatch(
        r"(?P<prefix>.*?^  items:\n)(?P<items>(?:^  - uid:.*(?:\n(?:    .*)?)*?)+)"
        r"(?P<suffix>^memberLayout:.*\n?)",
        content,
        flags=re.MULTILINE | re.DOTALL,
    )
    if match is None:
        raise ValueError(f"Unexpected DocFX API TOC structure: {toc_path}")

    blocks = re.findall(r"^  - uid:.*?(?=^  - uid:|\Z)", match.group("items"), re.MULTILINE | re.DOTALL)

    def sort_key(block: str) -> tuple[int, str]:
        uid_match = re.match(r"^  - uid: (.+)$", block, re.MULTILINE)
        if uid_match is None:
            raise ValueError(f"API TOC entry has no uid: {block!r}")
        uid = uid_match.group(1)
        return ORDER.get(uid, len(ORDER)), uid

    ordered = "".join(sorted(blocks, key=sort_key))
    toc_path.write_text(
        match.group("prefix") + ordered + match.group("suffix"),
        encoding="utf-8",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

from __future__ import annotations

import argparse
import json
from pathlib import Path

from crawler.engine import execute_request


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--request-file", required=True)
    args = parser.parse_args()

    request_path = Path(args.request_file)
    request = json.loads(request_path.read_text(encoding="utf-8-sig"))
    result = execute_request(request, base_dir=Path(__file__).parent / "crawler")
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()

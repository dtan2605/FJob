from __future__ import annotations

from pathlib import Path

from crawler.sources.selenium_source import SeleniumSearchSource


class Vieclam24hSeleniumSource(SeleniumSearchSource):
    def __init__(self, base_dir: Path) -> None:
        super().__init__(base_dir, website="vieclam24h", result_source_name="Vieclam24hSelenium")

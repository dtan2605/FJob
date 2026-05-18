from __future__ import annotations

from pathlib import Path

from crawler.sources.selenium_source import SeleniumSearchSource


class CareervietSeleniumSource(SeleniumSearchSource):
    def __init__(self, base_dir: Path) -> None:
        super().__init__(base_dir, website="careerviet", result_source_name="CareervietSelenium")

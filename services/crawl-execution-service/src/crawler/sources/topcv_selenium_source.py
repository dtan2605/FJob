from __future__ import annotations

from pathlib import Path

from crawler.sources.selenium_source import SeleniumSearchSource


class TopCvSeleniumSource(SeleniumSearchSource):
    def __init__(self, base_dir: Path) -> None:
        super().__init__(base_dir, website="topcv", result_source_name="TopcvSelenium")

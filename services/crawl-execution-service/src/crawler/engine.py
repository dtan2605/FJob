from __future__ import annotations

from crawler.sources.selenium_source import SeleniumSearchSource
from crawler.sources.topcv_selenium_source import TopCvSeleniumSource
from crawler.sources.vieclam24h_selenium_source import Vieclam24hSeleniumSource
from crawler.sources.careerviet_selenium_source import CareervietSeleniumSource
from crawler.sources.indeed_selenium_source import IndeedSeleniumSource


def execute_request(request: dict, base_dir) -> dict:
    source = request["source"].strip().lower()

    registry = {
        "topcv": TopCvSeleniumSource(base_dir),
        "careerviet": CareervietSeleniumSource(base_dir),
        "vieclam24h": Vieclam24hSeleniumSource(base_dir),
        "indeed": IndeedSeleniumSource(base_dir),
        "selenium": SeleniumSearchSource(base_dir),
        "all": SeleniumSearchSource(base_dir),
        "topcv-selenium": TopCvSeleniumSource(base_dir),
        "topcvselenium": TopCvSeleniumSource(base_dir),
        "careerviet-selenium": CareervietSeleniumSource(base_dir),
        "vieclam24h-selenium": Vieclam24hSeleniumSource(base_dir),
        "indeed-selenium": IndeedSeleniumSource(base_dir),
    }

    if source not in registry:
        raise ValueError(f"Unsupported source: {request['source']}")

    return registry[source].crawl(request)

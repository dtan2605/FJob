from __future__ import annotations


class RobotsPolicy:
    def __init__(self, allow_crawl: bool) -> None:
        self.allow_crawl = allow_crawl

    def ensure_allowed(self, source_name: str) -> None:
        if not self.allow_crawl:
            raise PermissionError(
                f"Crawling is disabled by robots/toS policy for source {source_name}."
            )

from __future__ import annotations

import re
from pathlib import Path
from typing import Any, Dict, List

from bs4 import BeautifulSoup

from crawler.filters import matches_filters
from crawler.http_client import fetch_url
from crawler.robots import RobotsPolicy
from crawler.tagging import derive_tags


def _extract_url(url: str, base_url: str) -> str:
    if not url:
        return ""
    if url.startswith("http"):
        return url
    return f"{base_url}{url}" if url.startswith("/") else f"{base_url}/{url}"


class IndeedSource:
    def __init__(self, base_dir: Path) -> None:
        self.base_dir = base_dir

    def crawl(self, request: dict) -> dict:
        RobotsPolicy(True).ensure_allowed("Indeed")

        keyword = request["keyword"].strip()
        location = request.get("location")
        salary_range = request.get("salaryRange")
        tags = request.get("tags", [])
        experience_level = request.get("experienceLevel")
        job_type = request.get("jobType")
        proxy_urls = request.get("proxyUrls")

        jobs = self._crawl_jobs(
            keyword,
            location,
            salary_range,
            tags,
            experience_level,
            job_type,
            proxy_urls,
        )

        return {
            "source": "Indeed",
            "keyword": request["keyword"],
            "strategy": "web-scraping",
            "traceId": request["traceId"],
            "jobs": jobs,
        }

    def _crawl_jobs(
        self,
        keyword: str,
        location: str | None = None,
        salary_range: str | None = None,
        tags: list[str] | None = None,
        experience_level: str | None = None,
        job_type: str | None = None,
        proxy_urls: list[str] | None = None,
    ) -> List[Dict[str, Any]]:
        base_url = "https://www.indeed.com.vn"
        params = [f"q={keyword.replace(' ', '+')}" ]
        if location:
            params.append(f"l={location.replace(' ', '+')}" )
        search_url = f"{base_url}/jobs?{'&'.join(params)}"

        try:
            current_url = search_url
            visited_pages = set()
            seen_urls = set()
            jobs: List[Dict[str, Any]] = []

            while current_url and current_url not in visited_pages:
                visited_pages.add(current_url)
                response = fetch_url(current_url, proxies=proxy_urls)
                soup = BeautifulSoup(response.content, "html.parser")
                cards = soup.select("div.job_seen_beacon, div.slider_container, a.tapItem")

                for card in cards:
                    job = self._parse_job_card(card, base_url)
                    if not job or not job.get("url") or job["url"] in seen_urls:
                        continue
                    if matches_filters(job, location, salary_range, tags, experience_level, job_type):
                        seen_urls.add(job["url"])
                        jobs.append(job)

                current_url = self._find_next_page_url(soup, base_url)

            return jobs
        except Exception as e:
            print(f"Error crawling Indeed: {e}")
            return []

    def _find_next_page_url(self, soup: BeautifulSoup, base_url: str) -> str:
        candidate = soup.select_one(
            'a[rel="next"], li.next a, a.next, .pagination a.next, .page-next a, a[aria-label*="Next"], a[aria-label*="Sau"]'
        )
        if candidate and candidate.get("href"):
            href = candidate["href"].strip()
            if href and not href.startswith("javascript") and href != "#":
                return _extract_url(href, base_url)

        for a in soup.find_all("a", href=True):
            text = a.get_text(" ", strip=True).lower()
            if not text:
                continue
            if any(token in text for token in ("next", "sau", ">", "»", "›", "→")):
                href = a["href"].strip()
                if href and not href.startswith("javascript") and href != "#":
                    return _extract_url(href, base_url)

        return ""

    def _parse_job_card(self, card: Any, base_url: str) -> Dict[str, Any] | None:
        try:
            title_elem = card.select_one("h2.jobTitle span")
            if not title_elem:
                title_elem = card.select_one("a.tapItem")
            if not title_elem:
                return None

            title = title_elem.get_text(strip=True)
            url_elem = card.select_one("a.tapItem") or card.select_one("a")
            url = ""
            if url_elem and url_elem.get("href"):
                href = url_elem["href"]
                url = href if href.startswith("http") else f"{base_url}{href}"

            company_elem = card.select_one("span.companyName")
            company = company_elem.get_text(strip=True) if company_elem else "Unknown"

            location_elem = card.select_one("div.companyLocation")
            location = location_elem.get_text(strip=True) if location_elem else "Unknown"

            salary_elem = card.select_one("div.metadata.salary-snippet-container, span.salaryText, span.salary-snippet")
            salary = salary_elem.get_text(strip=True) if salary_elem else "Negotiable"

            desc_elem = card.select_one("div.job-snippet")
            description = desc_elem.get_text(separator=" ", strip=True) if desc_elem else ""

            source_job_id = f"indeed-{hash(url) % 1000000}"
            return {
                "sourceJobId": source_job_id,
                "title": title,
                "company": company,
                "source": "Indeed",
                "url": url,
                "location": location,
                "salary": salary,
                "description": description,
                "tags": derive_tags(title, description, []),
                "postedAtUtc": "2026-05-10T00:00:00+00:00",
            }
        except Exception as e:
            print(f"Error parsing Indeed card: {e}")
            return None

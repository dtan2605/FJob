from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import List, Dict, Any

from bs4 import BeautifulSoup
from fake_useragent import UserAgent

from crawler.filters import matches_filters
from crawler.http_client import fetch_url
from crawler.robots import RobotsPolicy
from crawler.settings import read_env_int, read_int
from crawler.tagging import derive_tags


def _slugify(value: str) -> str:
    normalized = unicodedata.normalize("NFKD", value)
    ascii_text = "".join(ch for ch in normalized if not unicodedata.combining(ch))
    slug = re.sub(r"[^a-z0-9]+", "-", ascii_text.lower()).strip("-")
    return slug


def _extract_url(url: str, base_url: str) -> str:
    if not url:
        return ""
    if url.startswith("http"):
        return url
    return f"{base_url}{url}" if url.startswith("/") else f"{base_url}/{url}"


@dataclass(frozen=True)
class HttpCrawlLimits:
    max_pages: int
    max_jobs: int
    request_timeout_seconds: int
    post_page_delay_ms: int

    @classmethod
    def from_request(cls, request: dict) -> "HttpCrawlLimits":
        max_pages = read_int(
            request.get("maxPages"),
            read_env_int("CRAWL_MAX_PAGES", 3, minimum=1),
            minimum=1,
        )
        requested_max_jobs = request.get("maxJobs")
        default_max_jobs = read_env_int("CRAWL_MAX_JOBS", 60, minimum=1)
        inferred_max_jobs = max(max_pages * 30, default_max_jobs)
        return cls(
            max_pages=max_pages,
            max_jobs=read_int(
                requested_max_jobs,
                inferred_max_jobs,
                minimum=1,
            ),
            request_timeout_seconds=read_int(
                request.get("requestTimeoutSeconds"),
                read_env_int("HTTP_REQUEST_TIMEOUT_SECONDS", 15, minimum=1),
                minimum=1,
            ),
            post_page_delay_ms=read_int(
                request.get("postPageDelayMs"),
                read_env_int("HTTP_POST_PAGE_DELAY_MS", 0, minimum=0),
                minimum=0,
            ),
        )


class TopCvSource:
    def __init__(self, base_dir: Path) -> None:
        self.base_dir = base_dir
        self.ua = UserAgent()

    def crawl(self, request: dict) -> dict:
        RobotsPolicy(True).ensure_allowed("TopCV")
        limits = HttpCrawlLimits.from_request(request)

        keyword = request["keyword"].strip()
        location = request.get("location")
        salary_range = request.get("salaryRange")
        tags = request.get("tags", [])
        experience_level = request.get("experienceLevel")
        job_type = request.get("jobType")
        proxy_urls = request.get("proxyUrls")

        jobs = self._crawl_jobs(
            limits,
            keyword,
            location,
            salary_range,
            tags,
            experience_level,
            job_type,
            proxy_urls,
        )

        return {
            "source": "TopCV",
            "keyword": request["keyword"],
            "strategy": "web-scraping",
            "traceId": request["traceId"],
            "jobs": jobs,
        }

    def _crawl_jobs(
        self,
        limits: HttpCrawlLimits,
        keyword: str,
        location: str | None = None,
        salary_range: str | None = None,
        tags: list[str] | None = None,
        experience_level: str | None = None,
        job_type: str | None = None,
        proxy_urls: list[str] | None = None,
    ) -> List[Dict[str, Any]]:
        base_url = "https://www.topcv.vn"
        keyword_slug = _slugify(keyword)
        search_url = f"{base_url}/tim-viec-lam-{keyword_slug}"

        query_parts = []
        if location:
            location_map = {
                "hanoi": "ha-noi",
                "hà nội": "ha-noi",
                "ho chi minh": "ho-chi-minh",
                "sài gòn": "ho-chi-minh",
                "da nang": "da-nang",
                "đà nẵng": "da-nang",
            }
            location_code = location_map.get(location.lower(), location.lower().replace(" ", "-"))
            query_parts.append(f"location={location_code}")

        if salary_range:
            salary_map = {
                "under 3m": "1",
                "3-5m": "2",
                "5-7m": "3",
                "7-10m": "4",
                "10-15m": "5",
                "15-20m": "6",
                "20-30m": "7",
                "over 30m": "8",
                "negotiable": "",
            }
            salary_code = salary_map.get(salary_range.lower(), "")
            if salary_code:
                query_parts.append(f"salary={salary_code}")

        if experience_level:
            exp_map = {
                "entry level": "1",
                "junior": "1",
                "mid level": "3",
                "senior": "4",
                "executive": "5",
                "fresher": "1",
                "experienced": "3",
            }
            exp_code = exp_map.get(experience_level.lower(), "")
            if exp_code:
                query_parts.append(f"experience={exp_code}")

        if job_type:
            type_map = {
                "full time": "fulltime",
                "part time": "parttime",
                "freelance": "freelance",
                "internship": "internship",
                "intern": "internship",
            }
            type_code = type_map.get(job_type.lower(), "")
            if type_code:
                query_parts.append(f"job_type={type_code}")

        if query_parts:
            search_url = f"{search_url}?{'&'.join(query_parts)}"

        headers = {
            "User-Agent": self.ua.random,
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            "Accept-Language": "vi,en-US;q=0.7,en;q=0.3",
            "Connection": "keep-alive",
            "Upgrade-Insecure-Requests": "1",
        }

        try:
            current_url = search_url
            visited_pages = set()
            seen_urls = set()
            jobs = []

            while current_url and current_url not in visited_pages:
                if len(visited_pages) >= limits.max_pages or len(jobs) >= limits.max_jobs:
                    break

                visited_pages.add(current_url)
                response = fetch_url(
                    current_url,
                    headers=headers,
                    timeout=limits.request_timeout_seconds,
                    proxies=proxy_urls,
                )
                soup = BeautifulSoup(response.content, "html.parser")

                job_cards = self._find_job_cards(soup)
                if not job_cards:
                    job_cards = self._find_linked_jobs(soup, base_url)

                for card in job_cards:
                    job = self._parse_job_card(card, base_url)
                    if not job or not job.get("url") or job["url"] in seen_urls:
                        continue
                    if self._matches_filters(job, location, salary_range, tags, experience_level, job_type):
                        seen_urls.add(job["url"])
                        jobs.append(job)
                        if len(jobs) >= limits.max_jobs:
                            break

                if len(jobs) >= limits.max_jobs:
                    break

                current_url = self._find_next_page_url(soup, base_url)
                if current_url and limits.post_page_delay_ms > 0:
                    import time
                    time.sleep(limits.post_page_delay_ms / 1000)

            return jobs[:limits.max_jobs]

        except Exception as e:
            print(f"Error crawling TopCV: {e}")
            return []

    def _find_job_cards(self, soup: BeautifulSoup) -> list:
        selectors = [
            "div.job-item",
            "div.job-card",
            "article",
            "div.card",
            "div.search-result__item",
            "div.job-list__item",
            "div.tcv-item",
        ]
        for selector in selectors:
            items = soup.select(selector)
            if items:
                return items
        return []

    def _find_linked_jobs(self, soup: BeautifulSoup, base_url: str) -> list:
        anchors = []
        for a in soup.find_all("a", href=True):
            href = a["href"]
            if "/tim-viec-lam" in href or "/viec-lam" in href or "/topcv" in href:
                text = a.get_text(strip=True)
                if text:
                    anchors.append(a)
        return anchors

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
            title_elem = card.find("h3", class_=re.compile(r"title|job-title|name", re.I))
            if not title_elem:
                title_elem = card.find("a", href=True)
            if not title_elem:
                return None

            title = title_elem.get_text(strip=True)
            url = _extract_url(title_elem.get("href", ""), base_url)

            if not url and card.name == "a":
                url = _extract_url(card.get("href", ""), base_url)

            company_elem = card.find(class_=re.compile(r"company|employer|brand", re.I))
            company = company_elem.get_text(strip=True) if company_elem else "Unknown"

            location_elem = card.find(class_=re.compile(r"location|place|city", re.I))
            location = location_elem.get_text(strip=True) if location_elem else "Unknown"

            salary_elem = card.find(class_=re.compile(r"salary|wage|pay", re.I))
            salary = salary_elem.get_text(strip=True) if salary_elem else "Negotiable"

            desc_elem = card.find(class_=re.compile(r"description|summary|content|detail", re.I))
            description = desc_elem.get_text(" ", strip=True) if desc_elem else title

            source_job_id = f"topcv-{hash(url) % 1000000}"
            return {
                "sourceJobId": source_job_id,
                "title": title,
                "company": company,
                "source": "TopCV",
                "url": url,
                "location": location,
                "salary": salary,
                "description": description,
                "tags": derive_tags(title, description, []),
                "postedAtUtc": "2026-05-10T00:00:00+00:00",
            }

        except Exception as e:
            print(f"Error parsing TopCV card: {e}")
            return None

    def _matches_filters(
        self,
        job: Dict[str, Any],
        location: str | None = None,
        salary_range: str | None = None,
        tags: list[str] | None = None,
        experience_level: str | None = None,
        job_type: str | None = None,
    ) -> bool:
        return matches_filters(job, location, salary_range, tags, experience_level, job_type)

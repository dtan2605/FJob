from __future__ import annotations

import datetime
import os
import re
import shutil
import time
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional

import requests
from bs4 import BeautifulSoup
from fake_useragent import UserAgent
from selenium import webdriver
from selenium.common.exceptions import NoSuchElementException, TimeoutException, WebDriverException
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.support import expected_conditions as EC
from selenium.webdriver.support.ui import WebDriverWait
from webdriver_manager.chrome import ChromeDriverManager

from crawler.filters import matches_filters, normalize_text
from crawler.robots import RobotsPolicy
from crawler.settings import read_env_int, read_int
from crawler.tagging import derive_tags


def _extract_url(url: str, base_url: str) -> str:
    if not url:
        return ""
    if url.startswith("http"):
        return url
    return f"{base_url}{url}" if url.startswith("/") else f"{base_url}/{url}"


def _slugify(value: str) -> str:
    normalized = unicodedata.normalize("NFKD", value)
    ascii_text = "".join(ch for ch in normalized if not unicodedata.combining(ch))
    slug = re.sub(r"[^a-z0-9]+", "-", ascii_text.lower()).strip("-")
    return slug


@dataclass(frozen=True)
class SeleniumCrawlLimits:
    max_pages: int
    max_jobs: int
    page_load_timeout_seconds: int
    wait_timeout_seconds: int
    post_page_delay_ms: int
    browser_implicit_wait_seconds: int

    @classmethod
    def from_request(cls, request: dict) -> "SeleniumCrawlLimits":
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
            page_load_timeout_seconds=read_int(
                request.get("pageLoadTimeoutSeconds"),
                read_env_int("SELENIUM_PAGE_LOAD_TIMEOUT_SECONDS", 25, minimum=5),
                minimum=5,
            ),
            wait_timeout_seconds=read_int(
                request.get("waitTimeoutSeconds"),
                read_env_int("SELENIUM_WAIT_TIMEOUT_SECONDS", 8, minimum=1),
                minimum=1,
            ),
            post_page_delay_ms=read_int(
                request.get("postPageDelayMs"),
                read_env_int("SELENIUM_POST_PAGE_DELAY_MS", 500, minimum=0),
                minimum=0,
            ),
            browser_implicit_wait_seconds=read_int(
                request.get("browserImplicitWaitSeconds"),
                read_env_int("SELENIUM_IMPLICIT_WAIT_SECONDS", 2, minimum=0),
                minimum=0,
            ),
        )


class SeleniumSearchSource:
    _cached_driver_path: Path | None = None

    def __init__(
        self,
        base_dir: Path,
        website: str | None = None,
        result_source_name: str | None = None,
    ) -> None:
        self.base_dir = base_dir
        self.ua = UserAgent()
        self.website = (website or "").strip().lower()
        self.result_source_name = result_source_name

    def crawl(self, request: dict) -> dict:
        RobotsPolicy(True).ensure_allowed("Selenium")
        limits = SeleniumCrawlLimits.from_request(request)

        website = self.website or request.get("website", request.get("source", "")).strip().lower()
        keyword = request["keyword"].strip()
        location = request.get("location")
        salary_range = request.get("salaryRange")
        tags = request.get("tags", [])
        experience_level = request.get("experienceLevel")
        job_type = request.get("jobType")
        proxy_urls = request.get("proxyUrls")

        if website in {"topcv-selenium", "topcv"}:
            website = "topcv"
        elif website in {"careerviet-selenium", "careerviet"}:
            website = "careerviet"
        elif website in {"vieclam24h-selenium", "vieclam24h"}:
            website = "vieclam24h"
        elif website in {"indeed-selenium", "indeed"}:
            website = "indeed"
        elif website == "selenium":
            website = request.get("website", "topcv").strip().lower()

        if not website or website == "all":
            websites = ["topcv", "careerviet", "vieclam24h", "indeed"]
            all_jobs = []
            for w in websites:
                jobs = self._crawl_jobs(
                    w,
                    limits,
                    keyword,
                    location,
                    salary_range,
                    tags,
                    experience_level,
                    job_type,
                    proxy_urls,
                )
                all_jobs.extend(jobs)
            return {
                "source": self.result_source_name or "AllSelenium",
                "keyword": request["keyword"],
                "strategy": "selenium-web-scraping",
                "traceId": request["traceId"],
                "jobs": all_jobs,
            }
        else:
            jobs = self._crawl_jobs(
                website,
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
                "source": self.result_source_name or f"{website.capitalize()}Selenium",
                "keyword": request["keyword"],
                "strategy": "selenium-web-scraping",
                "traceId": request["traceId"],
                "jobs": jobs,
            }

    def _create_browser(self, limits: SeleniumCrawlLimits, proxy_url: str | None = None) -> webdriver.Chrome:
        options = Options()
        options.page_load_strategy = "eager"
        options.add_argument("--headless=new")
        options.add_argument("--disable-gpu")
        options.add_argument("--no-sandbox")
        options.add_argument("--disable-dev-shm-usage")
        options.add_argument("--disable-software-rasterizer")
        options.add_argument("--disable-background-timer-throttling")
        options.add_argument("--disable-backgrounding-occluded-windows")
        options.add_argument("--disable-renderer-backgrounding")
        options.add_argument("--window-size=1920,1080")
        options.add_argument("--disable-blink-features=AutomationControlled")
        options.add_argument("--disable-extensions")
        options.add_argument("--disable-infobars")
        options.add_argument("--blink-settings=imagesEnabled=false")
        options.add_argument(f"user-agent={self.ua.random}")
        options.add_experimental_option("excludeSwitches", ["enable-automation"])
        options.add_experimental_option("useAutomationExtension", False)
        options.add_experimental_option("prefs", {
            "profile.managed_default_content_settings.images": 2,
            "profile.default_content_setting_values.notifications": 2,
        })

        chrome_binary = self._detect_chrome_binary()
        if chrome_binary:
            options.binary_location = chrome_binary

        if proxy_url:
            options.add_argument(f"--proxy-server={proxy_url}")

        try:
            driver_path = self._get_driver_path()
            if not os.access(driver_path, os.X_OK):
                try:
                    driver_path.chmod(driver_path.stat().st_mode | 0o111)
                except Exception:
                    pass
            service = Service(str(driver_path))
            browser = webdriver.Chrome(service=service, options=options)
        except Exception as exc:
            print(f"Warning: WebDriverManager failed, falling back to Selenium-managed Chrome driver: {exc}")
            browser = webdriver.Chrome(options=options)

        browser.set_page_load_timeout(limits.page_load_timeout_seconds)
        browser.implicitly_wait(limits.browser_implicit_wait_seconds)
        browser.execute_cdp_cmd("Page.addScriptToEvaluateOnNewDocument", {
            "source": "Object.defineProperty(navigator, 'webdriver', {get: () => undefined})"
        })
        return browser

    @classmethod
    def _get_driver_path(cls) -> Path:
        if cls._cached_driver_path and cls._cached_driver_path.exists():
            return cls._cached_driver_path

        driver_path = Path(ChromeDriverManager().install())
        resolved_path = cls(Path("."))._resolve_chromedriver_path(driver_path)
        cls._cached_driver_path = resolved_path
        return resolved_path

    def _detect_chrome_binary(self) -> Optional[str]:
        candidates = [
            os.environ.get("CHROME_BINARY"),
            os.environ.get("GOOGLE_CHROME_SHIM"),
            shutil.which("chrome"),
            shutil.which("chromium"),
            shutil.which("chromium-browser"),
            shutil.which("msedge"),
            shutil.which("google-chrome"),
            shutil.which("google-chrome-stable"),
        ]

        known_paths = [
            r"C:\Program Files\Google\Chrome\Application\chrome.exe",
            r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            r"C:\Program Files\Chromium\Application\chrome.exe",
            "/usr/bin/google-chrome-stable",
            "/usr/bin/google-chrome",
            "/usr/bin/chromium-browser",
            "/usr/bin/chromium",
        ]

        for value in candidates + known_paths:
            if value and Path(value).exists():
                return str(value)
        return None

    def _resolve_chromedriver_path(self, driver_path: Path) -> Path:
        if driver_path.is_dir():
            candidate = next(
                (
                    p for p in driver_path.rglob("chromedriver*")
                    if p.is_file() and p.name.lower().startswith("chromedriver")
                ),
                None,
            )
            if candidate:
                return candidate
        if driver_path.name.lower().startswith("third_party_notices"):
            driver_dir = driver_path.parent
            candidate = next(
                (
                    p for p in driver_dir.iterdir()
                    if p.is_file() and p.name.lower().startswith("chromedriver")
                ),
                None,
            )
            if candidate:
                return candidate
        if driver_path.name.lower().endswith(".zip"):
            driver_dir = driver_path.parent
            candidate = next(
                (
                    p for p in driver_dir.rglob("chromedriver*")
                    if p.is_file() and p.name.lower().startswith("chromedriver")
                ),
                None,
            )
            if candidate:
                return candidate
        return driver_path

    def _crawl_jobs(
        self,
        website: str,
        limits: SeleniumCrawlLimits,
        keyword: str,
        location: str | None = None,
        salary_range: str | None = None,
        tags: list[str] | None = None,
        experience_level: str | None = None,
        job_type: str | None = None,
        proxy_urls: list[str] | None = None,
    ) -> List[Dict[str, Any]]:
        base_url = {
            "topcv": "https://www.topcv.vn",
            "careerviet": "https://careerviet.vn",
            "vieclam24h": "https://vieclam24h.vn",
            "indeed": "https://vn.indeed.com",
        }.get(website, "https://www.topcv.vn")

        search_url = self._build_search_url(website, base_url, keyword, location, salary_range)
        proxy_url = proxy_urls[0] if proxy_urls else None

        try:
            browser = self._create_browser(limits, proxy_url)
        except WebDriverException as exc:
            print(f"Error starting Selenium browser: {exc}")
            return []

        try:
            browser.get(search_url)
            self._wait_for_page_ready(browser, website, limits)

            if self._page_has_no_jobs(browser, website):
                self._perform_search_via_form(browser, website, keyword, location)
                self._wait_for_page_ready(browser, website, limits)

            unique_urls = set()
            all_jobs: List[Dict[str, Any]] = []
            visited_pages = set()

            while True:
                if len(visited_pages) >= limits.max_pages or len(all_jobs) >= limits.max_jobs:
                    break

                current_url = browser.current_url
                if current_url in visited_pages:
                    break
                visited_pages.add(current_url)

                html = browser.page_source
                page_jobs = self._extract_jobs_from_html(
                    html,
                    base_url,
                    website,
                    location,
                    salary_range,
                    tags,
                    experience_level,
                    job_type,
                    keyword,
                )
                if not page_jobs:
                    fallback_html = self._fetch_html_fallback(browser.current_url, website)
                    if fallback_html:
                        page_jobs = self._extract_jobs_from_html(
                            fallback_html,
                            base_url,
                            website,
                            location,
                            salary_range,
                            tags,
                            experience_level,
                            job_type,
                            keyword,
                        )
                for job in page_jobs:
                    if not job:
                        continue
                    if job["url"] and job["url"] in unique_urls:
                        continue
                    if job["url"]:
                        unique_urls.add(job["url"])
                    all_jobs.append(job)
                    if len(all_jobs) >= limits.max_jobs:
                        break

                if len(all_jobs) >= limits.max_jobs or not self._click_next_page(browser, website):
                    break
                self._wait_for_page_ready(browser, website, limits)

            return all_jobs[:limits.max_jobs]
        except Exception as exc:
            print(f"Error crawling with Selenium on {website}: {exc}")
            return []
        finally:
            browser.quit()

    def _fetch_html_fallback(self, url: str, website: str) -> str | None:
        if not url or website != "careerviet":
            return None

        try:
            response = requests.get(
                url,
                headers={"User-Agent": self.ua.random},
                timeout=20,
            )
            response.raise_for_status()
            return response.text
        except Exception:
            return None

    def _wait_for_page_ready(
        self,
        browser: webdriver.Chrome,
        website: str,
        limits: SeleniumCrawlLimits,
    ) -> None:
        expected_selectors = {
            "topcv": ["div.tcv-item", "div.job-item", "div.job-card", "article"],
            "careerviet": ["a.job_link", "div.job-item", "div.job-list-item", "div.post-item", "article"],
            "vieclam24h": ["a[data-job-id]", "div.item-job", "div.search-item", "div.job-card", "article"],
            "indeed": ["a.jcs-JobTitle", "a.tapItem", "div.job_seen_beacon", "div.slider_container", "div.jobsearch-SerpJobCard"],
        }.get(website, ["body"])

        for selector in expected_selectors:
            try:
                WebDriverWait(browser, limits.wait_timeout_seconds).until(
                    EC.presence_of_element_located((By.CSS_SELECTOR, selector))
                )
                break
            except TimeoutException:
                continue

        if limits.post_page_delay_ms > 0:
            time.sleep(limits.post_page_delay_ms / 1000)

    def _perform_search_via_form(self, browser: webdriver.Chrome, website: str, keyword: str, location: str | None) -> bool:
        if website == "indeed":
            return self._perform_indeed_search(browser, keyword, location)
        
        keyword_input = self._find_input_element(browser, [
            "input[name*=keyword]",
            "input[id*=keyword]",
            "input[placeholder*=tìm]",
            "input[placeholder*=job]",
            "input[placeholder*=công việc]",
            "input[name*=q]",
        ])

        if not keyword_input:
            keyword_input = self._find_input_element(browser, [
                "input[type=text]",
                "input[type=search]",
            ])

        if not keyword_input:
            return False

        try:
            keyword_input.clear()
            keyword_input.send_keys(keyword)

            if location:
                location_input = self._find_input_element(browser, [
                    "input[name*=location]",
                    "input[id*=location]",
                    "input[placeholder*=địa điểm]",
                    "input[placeholder*=tỉnh]",
                    "input[placeholder*=thành phố]",
                ])
                if location_input:
                    location_input.clear()
                    location_input.send_keys(location)

            button = self._find_clickable(browser, [
                "button[type=submit]",
                "button[aria-label*=tìm]",
                "button[class*=search]",
                "button[class*=tim]",
                "a[role=button]",
            ])
            if button:
                button.click()
                return True

            keyword_input.send_keys(Keys.RETURN)
            return True
        except Exception:
            return False

    def _perform_indeed_search(self, browser: webdriver.Chrome, keyword: str, location: str | None) -> bool:
        try:
            # Indeed uses specific input names
            keyword_input = self._find_input_element(browser, [
                "input[name='q']",
                "input[id='text-input-what']",
                "input[placeholder*='job']",
                "input[placeholder*='việc']",
            ])

            if keyword_input:
                keyword_input.clear()
                keyword_input.send_keys(keyword)

            if location:
                location_input = self._find_input_element(browser, [
                    "input[name='l']",
                    "input[id='text-input-where']",
                    "input[placeholder*='location']",
                    "input[placeholder*='địa điểm']",
                ])
                if location_input:
                    location_input.clear()
                    location_input.send_keys(location)

            # Find and click the search button
            search_button = self._find_clickable(browser, [
                "button[type='submit']",
                "button[id*='search']",
                "button[class*='search']",
                "button[aria-label*='Search']",
            ])
            
            if search_button:
                search_button.click()
                return True

            # If no button found, try pressing Enter on keyword input
            if keyword_input:
                keyword_input.send_keys(Keys.RETURN)
                return True

            return False
        except Exception:
            return False

    def _find_input_element(self, browser: webdriver.Chrome, selectors: list[str]) -> Optional[webdriver.remote.webelement.WebElement]:
        for selector in selectors:
            try:
                elements = browser.find_elements(By.CSS_SELECTOR, selector)
                if elements:
                    return elements[0]
            except Exception:
                continue
        return None

    def _find_clickable(self, browser: webdriver.Chrome, selectors: list[str]) -> Optional[webdriver.remote.webelement.WebElement]:
        for selector in selectors:
            try:
                elements = browser.find_elements(By.CSS_SELECTOR, selector)
                for element in elements:
                    if element.is_displayed() and element.is_enabled():
                        return element
            except Exception:
                continue
        return None

    def _build_search_url(self, website: str, base_url: str, keyword: str, location: str | None, salary_range: str | None) -> str:
        keyword_fragment = _slugify(keyword)
        if website == "topcv":
            return f"{base_url}/tim-viec-lam-{keyword_fragment}"
        if website == "careerviet":
            return f"{base_url}/viec-lam/{keyword_fragment}-k-vi.html"
        if website == "vieclam24h":
            params = [f"q={keyword.replace(' ', '+')}" ]
            if location:
                params.append(f"address={location.replace(' ', '+')}" )
            return f"{base_url}/tim-kiem-viec-lam-nhanh?{'&'.join(params)}"
        if website == "indeed":
            params = [f"q={keyword.replace(' ', '+')}" ]
            if location:
                params.append(f"l={location.replace(' ', '+')}" )
            return f"{base_url}/jobs?{'&'.join(params)}"
        return f"{base_url}/tim-viec-lam-{keyword_fragment}"

    def _page_has_no_jobs(self, browser: webdriver.Chrome, website: str) -> bool:
        soup = BeautifulSoup(browser.page_source, "html.parser")
        cards = self._find_job_cards(soup, website)
        return not bool(cards)

    def _extract_jobs_from_html(
        self,
        html: str,
        base_url: str,
        website: str,
        location: str | None,
        salary_range: str | None,
        tags: list[str] | None,
        experience_level: str | None,
        job_type: str | None,
        keyword: str | None = None,
    ) -> List[Dict[str, Any]]:
        soup = BeautifulSoup(html, "html.parser")
        cards = self._find_job_cards(soup, website)
        if not cards:
            cards = self._find_linked_jobs(soup, base_url, website)

        jobs: List[Dict[str, Any]] = []
        for card in cards:
            job = self._parse_job_card(card, base_url, website)
            # Trả hết toàn bộ job mà không lọc, cho phép search service quyết định
            if job and self._matches_keyword(job, keyword):
                jobs.append(job)
        return jobs

    def _matches_keyword(self, job: Dict[str, Any], keyword: str | None) -> bool:
        normalized_keyword = normalize_text(keyword)
        if not normalized_keyword:
            return True

        synonym_map = {
            "intern": ["intern", "internship", "thuc tap"],
            "dotnet": ["dotnet", "asp net", "c#"],
            "it": ["it", "cntt", "cong nghe thong tin"],
        }

        combined_text = normalize_text(
            " ".join(
                [
                    str(job.get("title", "")),
                    str(job.get("description", "")),
                    str(job.get("company", "")),
                    " ".join(job.get("tags", []) if isinstance(job.get("tags"), list) else []),
                ]
            )
        )

        terms = [term for term in normalized_keyword.split() if term]
        if not terms:
            return True

        for term in terms:
            variants = synonym_map.get(term, [term])
            if not any(self._contains_term(combined_text, variant) for variant in variants):
                return False

        return True

    def _contains_term(self, text: str, term: str) -> bool:
        normalized_text = normalize_text(text)
        normalized_term = normalize_text(term)
        if not normalized_text or not normalized_term:
            return False

        parts = [re.escape(part) for part in normalized_term.split() if part]
        if not parts:
            return False

        pattern = r"(?<![a-z0-9])" + r"[\s-]+".join(parts) + r"(?![a-z0-9])"
        return re.search(pattern, normalized_text) is not None

    def _find_job_cards(self, soup: BeautifulSoup, website: str) -> list:
        selectors_by_site = {
            "topcv": [
                "div.job-item-search-result",
                "div.job-list-search-result div[data-job-id]",
                "div.tcv-item",
                "div.job-card",
                "div[itemprop='jobPosting']",
                "article",
            ],
            "careerviet": [
                "div.job-item",
                "a.job_link[href*='/vi/tim-viec-lam/']",
                "div.job-list-item",
                "div.post-item",
                "article",
            ],
            "vieclam24h": [
                "a[data-job-id]",
                "a[href*='/duoc-pham/']",
                "div.item-job",
                "div.search-item",
                "div.job-card",
                "article",
            ],
            "indeed": [
                "a.jcs-JobTitle",
                "a.tapItem",
                "div.job_seen_beacon",
                "div.slider_container",
                "div.jobsearch-SerpJobCard",
            ]
        }
        selectors = selectors_by_site.get(website, [
            "div.job-item-search-result",
            "div.job-list-search-result",
            "div.list-job",
            "div.box-job-list",
            "div.job-item",
            "div.job-card",
            "article",
            "li.job",
            "div.job-list-item",
            "div.post-item",
            "div.search-item",
            "div.tcv-item",
            "div.item-job",
            "div[data-job-id]",
            "div.job-card__item",
            "div.list-item",
            "div.board-item",
            "div.slider_container",
            "div.job_seen_beacon",
            "a.tapItem",
        ])

        for selector in selectors:
            items = soup.select(selector)
            if items:
                return items
        return []

    def _find_linked_jobs(self, soup: BeautifulSoup, base_url: str, website: str) -> list:
        anchors = []
        for a in soup.find_all("a", href=True):
            href = a["href"]
            if website == "indeed":
                if href.startswith("/rc/clk") or "/pagead/" in href or "/company/" in href or href.startswith("/pagead/"):
                    anchors.append(a)
            elif website == "topcv":
                if "/viec-lam/" in href:
                    anchors.append(a)
            elif website == "careerviet":
                if (
                    href.startswith("/vi/tim-viec-lam/")
                    or href.startswith("/tim-viec-lam/")
                    or href.startswith("/viec-lam/")
                ) and "/nha-tuyen-dung/" not in href:
                    anchors.append(a)
            elif website == "vieclam24h":
                if href.startswith("/duoc-pham/") or href.startswith("/viec-lam/") or href.startswith("/tim-viec-lam/") or "/tim-kiem-viec-lam-nhanh" in href:
                    anchors.append(a)
            else:
                if "/viec-lam/" in href or "/tim-viec-lam" in href or "/topcv" in href or "/jobs/" in href or "/job/" in href:
                    anchors.append(a)
        return anchors

    def _parse_job_card(self, card: Any, base_url: str, source: str) -> Dict[str, Any] | None:
        try:
            title_text = None
            url = ""
            company = "Unknown"
            location = "Unknown"
            salary = "Negotiable"
            description = ""

            if source == "topcv":
                link_elem = (
                    card.select_one("a[href*='/viec-lam/']")
                    or card.select_one(".title a[href]")
                    or card.select_one("a[href]")
                )
                title_elem = (
                    card.select_one(".title span")
                    or card.select_one(".title")
                    or card.select_one("span[data-original-title]")
                    or link_elem
                )
                company_elem = (
                    card.select_one(".company-name")
                    or card.select_one(".company")
                    or card.select_one("[class*='company']")
                )
                location_elem = (
                    card.select_one(".box-salary-and-address__address")
                    or card.select_one(".address")
                    or card.select_one("[class*='address']")
                )
                salary_elem = (
                    card.select_one(".box-salary-and-address__salary")
                    or card.select_one(".salary")
                    or card.select_one("[class*='salary']")
                )
                desc_elem = (
                    card.select_one(".box-job-relevance-job_text")
                    or card.select_one(".job-description")
                )
            elif source == "indeed":
                indeed_containers = [card]
                parent = getattr(card, "parent", None)
                while parent is not None and len(indeed_containers) < 5:
                    indeed_containers.append(parent)
                    parent = getattr(parent, "parent", None)

                def _select_from_indeed(selectors: list[str]) -> Any:
                    for container in indeed_containers:
                        if not getattr(container, "select_one", None):
                            continue
                        for selector in selectors:
                            element = container.select_one(selector)
                            if element:
                                return element
                    return None

                title_elem = (
                    _select_from_indeed([
                        "h2.jobTitle a span",
                        "h2.jobTitle span[title]",
                        "a.jcs-JobTitle span",
                        "a.tapItem span[title]",
                    ])
                    or _select_from_indeed(["h2.jobTitle", "a.jcs-JobTitle", "a.tapItem"])
                )
                link_elem = (
                    card if card.name == "a" and card.get("href") else None
                ) or _select_from_indeed([
                    "a.jcs-JobTitle[href]",
                    "a.tapItem[href]",
                    "h2.jobTitle a[href]",
                    "a[href*='/viewjob']",
                ])
                company_elem = _select_from_indeed([
                    "span.companyName",
                    "[data-testid='company-name']",
                    "span[data-testid='company-name']",
                    "span.company",
                ])
                location_elem = _select_from_indeed([
                    "div.companyLocation",
                    "span.companyLocation",
                    "[data-testid='text-location']",
                    "[data-testid='job-location']",
                ])
                salary_elem = _select_from_indeed([
                    "div.metadata.salary-snippet-container",
                    "span.salaryText",
                    "div.salary-snippet",
                    "[data-testid='attribute_snippet_testid']",
                ])
                desc_elem = _select_from_indeed([
                    "div.job-snippet",
                    "[data-testid='job-snippet']",
                ])
            elif source == "careerviet":
                link_elem = (
                    card.select_one("a.job_link[href*='/vi/tim-viec-lam/']")
                    or card.select_one("h2 a[href*='/vi/tim-viec-lam/']")
                    or card.select_one("a[href*='/vi/tim-viec-lam/']")
                )
                title_elem = card.select_one("h2 a.job_link") or card.select_one("h2") or link_elem or card.select_one(".job-title")
                company_elem = card.select_one(".company") or card.select_one(".employer") or card.select_one("span.brand") or card.select_one(".company-name")
                location_elem = card.select_one(".location") or card.select_one(".city") or card.select_one("span.place") or card.select_one(".location-name")
                salary_elem = card.select_one(".salary") or card.select_one(".job-salary")
                desc_elem = card.select_one(".description") or card.select_one(".job-summary")
            elif source == "vieclam24h":
                title_elem = None
                company_elem = None
                location_elem = None
                salary_elem = None
                desc_elem = None
                if card.name == "a":
                    link_elem = card
                else:
                    link_elem = card.select_one("a[data-job-id], a[href*='/duoc-pham/'], a[href*='/viec-lam/']")
                text_lines = [line.strip() for line in card.get_text("\n", strip=True).splitlines() if line.strip()]
                if text_lines:
                    title_text = text_lines[0]
                    company = text_lines[1] if len(text_lines) > 1 else "Unknown"
                    salary = text_lines[2] if len(text_lines) > 2 else "Negotiable"
                    location = text_lines[3] if len(text_lines) > 3 else "Unknown"
                    description = " ".join(text_lines)
                else:
                    title_text = card.get_text(strip=True)
                    company = "Unknown"
                    salary = "Negotiable"
                    location = "Unknown"
                    description = title_text
            else:
                title_elem = card.find("h2") or card.find("h3")
                link_elem = card if card.name == "a" and card.get("href") else card.select_one("a[href]")
                company_elem = card.find(class_=re.compile(r"company|employer|brand|name", re.I))
                location_elem = card.find(class_=re.compile(r"address|province|city|location|place", re.I))
                salary_elem = card.find(class_=re.compile(r"salary|wage|pay|income|triệu|thoả thuận|thỏa thuận", re.I))
                desc_elem = card.find(class_=re.compile(r"description|summary|info|content|detail|box-job|job-description", re.I))

            if title_elem:
                title_text = title_elem.get_text(strip=True)

            if title_elem and title_elem.name == "a" and title_elem.get("href"):
                link_elem = title_elem
            elif not link_elem:
                candidates = [a for a in card.find_all("a", href=True) if a.get_text(strip=True)]
                link_elem = candidates[0] if candidates else card.find("a", href=True)

            if link_elem and link_elem.get("href"):
                url = _extract_url(link_elem.get("href", ""), base_url)

            if not title_text and link_elem:
                title_text = link_elem.get_text(strip=True)

            if not title_text:
                title_elem = card.find(class_=re.compile(r"title|job-title|name|job-name", re.I))
                title_text = title_elem.get_text(strip=True) if title_elem else None

            if not title_text:
                return None

            if source == "careerviet" and "/tim-viec-lam/" not in url:
                return None

            if not url and card.name == "a":
                url = _extract_url(card.get("href", ""), base_url)

            if not company_elem:
                company_elem = card.find(class_=re.compile(r"company|employer|brand|name", re.I))
            if company_elem:
                company = company_elem.get_text(strip=True)

            if not location_elem:
                location_elem = card.find(class_=re.compile(r"address|province|city|location|place", re.I))
            if location_elem:
                location = location_elem.get_text(strip=True)

            if not salary_elem:
                salary_elem = card.find(class_=re.compile(r"salary|wage|pay|income|triệu|thoả thuận|thỏa thuận", re.I))
            if salary_elem:
                salary = salary_elem.get_text(strip=True)

            if source == "vieclam24h":
                text_lines = [line.strip() for line in card.get_text("\n", strip=True).splitlines() if line.strip()]
                if text_lines:
                    if not title_text:
                        title_text = text_lines[0]
                    if company == "Unknown" and len(text_lines) > 1:
                        company = text_lines[1]
                    if salary == "Negotiable" and len(text_lines) > 2 and any(ch.isdigit() for ch in text_lines[2]):
                        salary = text_lines[2]
                    if location == "Unknown" and len(text_lines) > 3:
                        location = text_lines[3]
                    if not description:
                        description = " ".join(text_lines)

            if not desc_elem:
                desc_elem = card.find(class_=re.compile(r"description|summary|info|content|detail|box-job|job-description", re.I))
            if desc_elem:
                description = desc_elem.get_text(" ", strip=True)

            if not description:
                description = title_text

            source_job_id = f"{source.lower()}-{abs(hash(url or title_text)) % 1000000}"
            return {
                "sourceJobId": source_job_id,
                "title": title_text,
                "company": company,
                "source": source.capitalize(),
                "url": url,
                "location": location,
                "salary": salary,
                "description": description,
                "tags": derive_tags(title_text, description, []),
                "postedAtUtc": datetime.datetime.utcnow().replace(tzinfo=datetime.timezone.utc).isoformat(),
            }
        except Exception as exc:
            print(f"Error parsing Selenium job card: {exc}")
            return None

    def _click_next_page(self, browser: webdriver.Chrome, website: str) -> bool:
        selectors = [
            "a[rel='next']",
            "a.next",
            "button.next",
            "a[aria-label*='Next']",
            "a[aria-label*='next']",
            "button[aria-label*='Next']",
            ".pagination a",
            ".pager a",
            "li.page-item a",
            "a[data-testid*='next-page']",  # Indeed specific
            "a[aria-label*='Next Page']",   # Indeed specific
            "span[title='Next'] a",         # Indeed specific
        ]

        candidates = []
        for selector in selectors:
            try:
                candidates.extend(browser.find_elements(By.CSS_SELECTOR, selector))
            except Exception:
                continue

        for element in candidates:
            try:
                text = (element.text or "").strip().lower()
                if not text:
                    continue
                if any(keyword in text for keyword in ["next", ">", "›", "»", "sau"]):
                    browser.execute_script("arguments[0].scrollIntoView({block:'center'})", element)
                    element.click()
                    return True
            except Exception:
                continue

        try:
            page_links = browser.find_elements(By.CSS_SELECTOR, "nav a")
            for element in page_links:
                text = (element.text or "").strip().lower()
                if text in {">", "›", "»", "next", "sau"}:
                    browser.execute_script("arguments[0].scrollIntoView({block:'center'})", element)
                    element.click()
                    return True
        except Exception:
            pass

        return False

from __future__ import annotations

import os
import random
import urllib.parse
from typing import Any, Dict, Iterable, List, Optional

import requests
from fake_useragent import UserAgent

try:
    import cloudscraper
except ModuleNotFoundError:
    cloudscraper = None

USER_AGENT = UserAgent()


def build_headers(extra_headers: Optional[Dict[str, str]] = None, referer: str | None = None) -> Dict[str, str]:
    headers = {
        "User-Agent": USER_AGENT.random,
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "vi,en-US;q=0.7,en;q=0.3",
        "Accept-Encoding": "gzip, deflate, br",
        "Connection": "keep-alive",
        "Upgrade-Insecure-Requests": "1",
        "Referer": referer or "https://www.google.com/",
    }
    if extra_headers:
        headers.update(extra_headers)
    return headers


def _parse_proxy_list(proxy_value: str | None) -> List[str]:
    if not proxy_value:
        return []
    return [item.strip() for item in proxy_value.split(",") if item.strip()]


def load_proxies() -> List[str]:
    proxies = _parse_proxy_list(os.getenv("CRAWL_PROXY_LIST"))
    if not proxies:
        proxy = os.getenv("CRAWL_PROXY")
        if proxy:
            proxies = [proxy.strip()]
    return proxies


def _build_request_proxies(proxy_url: str) -> Dict[str, str]:
    return {"http": proxy_url, "https": proxy_url}


def _is_cloudflare_challenge(response: requests.Response) -> bool:
    text = response.text.lower()
    if response.status_code in {403, 429, 503}:
        return True
    return (
        "attention required" in text
        or "just a moment" in text
        or ("cloudflare" in text and "browser check" in text)
        or "blocked - indeed.com" in text
        or "captcha" in text
        or "verify you are human" in text
        or "request blocked" in text
    )

FALLBACK_SERVICES = [
    "https://api.allorigins.win/raw?url=",
    "https://api.allorigins.cf/raw?url=",
    "https://api.codetabs.com/v1/proxy?quest=",
]


def _build_fallback_url(url: str, service: str) -> str:
    escaped = urllib.parse.quote_plus(url)
    return f"{service}{escaped}"


def _send_request(url: str, headers: Dict[str, str], timeout: int, proxy_url: str | None = None) -> requests.Response:
    request_args = {
        "url": url,
        "headers": headers,
        "timeout": timeout,
        "verify": True,
        "allow_redirects": True,
    }
    if proxy_url:
        request_args["proxies"] = _build_request_proxies(proxy_url)
    response = requests.get(**request_args)
    if _is_cloudflare_challenge(response):
        raise requests.exceptions.HTTPError("Cloudflare challenge detected", response=response)
    if response.status_code >= 400:
        response.raise_for_status()
    return response


def _send_cloudscraper_request(url: str, headers: Dict[str, str], timeout: int) -> requests.Response:
    if cloudscraper is None:
        raise RuntimeError("cloudscraper is not installed")

    scraper = cloudscraper.create_scraper(
        browser={"custom": headers.get("User-Agent", USER_AGENT.random)},
        allow_brotli=True,
    )
    response = scraper.get(url, timeout=timeout, headers=headers, allow_redirects=True)
    if _is_cloudflare_challenge(response):
        raise requests.exceptions.HTTPError("Cloudflare challenge detected", response=response)
    if response.status_code >= 400:
        response.raise_for_status()
    return response


def fetch_url(
    url: str,
    headers: Optional[Dict[str, str]] = None,
    timeout: int = 30,
    proxies: Optional[Iterable[str]] = None,
    allow_fallback: bool = True,
) -> requests.Response:
    headers = build_headers(headers, referer=url)
    candidate_proxies = list(proxies or [])
    candidate_proxies.extend(load_proxies())

    last_error: Optional[Exception] = None
    for proxy_url in candidate_proxies:
        try:
            return _send_request(url, headers, timeout, proxy_url)
        except Exception as exc:
            last_error = exc
            continue

    try:
        return _send_request(url, headers, timeout)
    except Exception as direct_exc:
        last_error = direct_exc
        if cloudscraper is not None:
            try:
                return _send_cloudscraper_request(url, headers, timeout)
            except Exception as cloud_exc:
                last_error = cloud_exc

        if not allow_fallback:
            raise last_error from direct_exc if last_error else direct_exc

        for service in FALLBACK_SERVICES:
            try:
                fallback_url = _build_fallback_url(url, service)
                response = _send_request(fallback_url, headers, timeout)
                return response
            except Exception as fallback_exc:
                last_error = fallback_exc
                continue

        raise last_error from direct_exc if last_error else direct_exc

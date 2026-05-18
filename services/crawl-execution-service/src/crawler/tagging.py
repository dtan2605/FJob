from __future__ import annotations

import re
import unicodedata


SYNONYMS = {
    "intern": ["intern", "internship", "thuc tap", "thuc-tap"],
    "python": ["python"],
    "dotnet": [".net", "dotnet", "asp.net", "c#"],
    "it": ["it", "cntt", "cong nghe thong tin"],
}


def _normalize(value: str) -> str:
    normalized = unicodedata.normalize("NFKD", value or "")
    ascii_text = "".join(ch for ch in normalized if not unicodedata.combining(ch))
    return ascii_text.strip().lower()


def _contains_keyword(text: str, keyword: str) -> bool:
    normalized_text = _normalize(text)
    normalized_keyword = _normalize(keyword)
    if not normalized_text or not normalized_keyword:
        return False

    parts = [re.escape(part) for part in normalized_keyword.split() if part]
    if not parts:
        return False

    pattern = r"(?<![a-z0-9])" + r"[\s-]+".join(parts) + r"(?![a-z0-9])"
    return re.search(pattern, normalized_text) is not None


def derive_tags(title: str, description: str, existing_tags: list[str]) -> list[str]:
    combined = f"{title} {description}"
    tags = {tag.strip().lower() for tag in existing_tags if tag.strip()}

    for canonical, keywords in SYNONYMS.items():
        if any(_contains_keyword(combined, keyword) for keyword in keywords):
            tags.add(canonical)

    return sorted(tags)

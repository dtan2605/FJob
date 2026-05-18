from __future__ import annotations

import re
import unicodedata
from typing import Dict, List, Optional


def normalize_text(value: str | None) -> str:
    if not value:
        return ""
    normalized = unicodedata.normalize("NFKD", value)
    ascii_text = "".join(ch for ch in normalized if not unicodedata.combining(ch))
    return ascii_text.strip().lower()


def salary_in_range(job_salary: str, requested_range: str) -> bool:
    numbers = re.findall(r"\d+", job_salary.replace(',', '').replace('.', ''))
    if not numbers:
        return False

    try:
        salary_value = int(numbers[0]) * 1000000
    except ValueError:
        return False

    range_map = {
        'under 3m': lambda x: x < 3000000,
        '3-5m': lambda x: 3000000 <= x <= 5000000,
        '5-7m': lambda x: 5000000 <= x <= 7000000,
        '7-10m': lambda x: 7000000 <= x <= 10000000,
        '10-15m': lambda x: 10000000 <= x <= 15000000,
        '15-20m': lambda x: 15000000 <= x <= 20000000,
        '20-30m': lambda x: 20000000 <= x <= 30000000,
        'over 30m': lambda x: x > 30000000,
    }

    check = range_map.get(requested_range.lower())
    return check(salary_value) if check else False


def matches_filters(
    job: Dict[str, object],
    location: str | None = None,
    salary_range: str | None = None,
    tags: List[str] | None = None,
    experience_level: str | None = None,
    job_type: str | None = None,
) -> bool:
    filter_checks: List[bool] = []
    title = normalize_text(job.get('title', '') if isinstance(job.get('title', ''), str) else '')
    description = normalize_text(job.get('description', '') if isinstance(job.get('description', ''), str) else '')
    location_text = normalize_text(job.get('location', '') if isinstance(job.get('location', ''), str) else '')
    salary_text = normalize_text(job.get('salary', '') if isinstance(job.get('salary', ''), str) else '')
    tags_text = normalize_text(' '.join(job.get('tags', []) if isinstance(job.get('tags', []), list) else []))
    combined_text = ' '.join([title, description, location_text, tags_text])

    if location:
        filter_checks.append(location.lower() in location_text)

    if salary_range:
        if 'negotiable' in salary_range.lower() and 'thỏa thuận' in salary_text:
            filter_checks.append(True)
        else:
            filter_checks.append(
                salary_range.lower() in salary_text or salary_in_range(salary_text, salary_range)
            )

    if tags:
        filter_checks.append(any(tag.lower() in combined_text for tag in tags))

    if experience_level:
        exp_keywords = {
            'entry level': ['fresher', 'mới ra trường', 'entry'],
            'junior': ['junior', '1-2 năm', 'junior'],
            'mid level': ['2-5 năm', 'middle', 'mid'],
            'senior': ['senior', '5-10 năm', 'trên 5 năm'],
            'executive': ['manager', 'director', 'executive', 'trên 10 năm'],
        }
        required_keywords = exp_keywords.get(experience_level.lower(), [])
        filter_checks.append(bool(required_keywords and any(keyword in combined_text for keyword in required_keywords)))

    if job_type:
        type_keywords = {
            'full time': ['fulltime', 'toàn thời gian'],
            'part time': ['parttime', 'bán thời gian'],
            'freelance': ['freelance', 'tự do'],
            'internship': ['intern', 'thực tập', 'internship'],
        }
        required_keywords = type_keywords.get(job_type.lower(), [])
        filter_checks.append(bool(required_keywords and any(keyword in combined_text for keyword in required_keywords)))

    return True if not filter_checks else any(filter_checks)

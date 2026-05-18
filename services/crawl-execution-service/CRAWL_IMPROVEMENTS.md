# Crawl Execution Service - Improvements & Features

## Overview
The crawl execution service has been significantly improved to reliably scrape job listings from Vietnamese job sites: **TopCV**, **Careerviet**, **Vieclam24h**, and **Indeed**.

## 📋 Supported Sources

### 1. **TopCV** (https://www.topcv.vn)
- **Status**: ✓ Improved with robust fallback selectors
- **URL Pattern**: `/tim-viec-lam-{keyword-slugified}?filters`
- **Key Features**:
  - Slug-based keyword encoding (Vietnamese handling)
  - Cloudflare bypass via `cloudscraper`
  - Multi-selector parsing for flexible HTML matching
  - Location, salary, experience level, job type filters

### 2. **Careerviet** (https://careerviet.vn)
- **Status**: ✓ Rewritten with flexible parsing
- **URL Pattern**: `/tim-viec-lam/{keyword}?filters`
- **Key Features**:
  - Multi-selector cascade (div.job-item → div.job-card → linked jobs)
  - Regex-based class matching for resilience
  - Fallback to anchor tag extraction for incomplete pages
  - Full filter support

### 3. **Vieclam24h** (https://vieclam24h.vn)
- **Status**: ✓ Rewritten with flexible parsing
- **URL Pattern**: `/tim-viec-lam/{keyword}?filters`
- **Key Features**:
  - Multiple selector strategies
  - Linked job extraction fallback
  - Supports data attributes (e.g., `div[data-job-id]`)
  - Filter integration

### 4. **Indeed** (https://www.indeed.com)
- **Status**: ✓ Working baseline
- **URL Pattern**: `/jobs?q={keyword}&l={location}`
- **Key Features**:
  - Direct HTML scraping (no Cloudflare)
  - Company, location, salary extraction
  - Experience level matching

## 🔧 Technical Architecture

### HTTP Client (`http_client.py`)
- **Proxy Support**: Environment-based proxy list loading
  - `CRAWL_PROXY_LIST`: comma-separated proxy URLs
  - `CRAWL_PROXY`: single fallback proxy
- **Cloudflare Detection**:
  - Detects CF challenge responses (403, 429, 503)
  - Automatic `cloudscraper` fallback
  - Cloudflare-specific keyword detection
- **Retry Strategy**:
  1. Try with each available proxy
  2. Try direct connection
  3. Try `cloudscraper` if installed
  4. Try fallback proxy services (AllOrigins, CodeTabs)
- **Headers**: Realistic User-Agent, Accept, Accept-Language, Referer

### Source Classes
All sources follow the same structure:
```python
class {Source}Source:
    def crawl(request: dict) -> dict
    def _crawl_jobs(...) -> List[Dict]
    def _find_job_cards(soup) -> list        # Multi-selector parsing
    def _find_linked_jobs(soup) -> list      # Fallback extraction
    def _parse_job_card(card) -> Dict | None # Flexible field extraction
    def _matches_filters(...) -> bool        # Filter matching
```

### Parser Robustness
- **Selector Cascade**: Try multiple CSS/class patterns
- **Regex Class Matching**: Match common patterns (company, location, salary)
- **Fallback Extraction**: Extract from `<a>` tags if structured data missing
- **Error Handling**: Graceful degradation on parse failures

## 📦 Installation & Setup

### Requirements
```bash
pip install requests beautifulsoup4 fake-useragent cloudscraper
```

### Environment Variables (Optional)
```bash
# Proxy configuration
export CRAWL_PROXY_LIST="http://proxy1:port,http://proxy2:port"
export CRAWL_PROXY="http://fallback-proxy:port"
```

## 🚀 Usage

### Basic Request Format
```python
request = {
    "source": "TopCV",           # TopCV, Careerviet, Vieclam24h, Indeed
    "keyword": "Python",
    "location": "Ha Noi",        # Optional
    "salaryRange": "5-7m",       # Optional: under 3m, 3-5m, 5-7m, etc.
    "jobType": "full time",      # Optional: full time, part time, freelance, internship
    "experienceLevel": "senior", # Optional: fresher, junior, mid level, senior, executive
    "tags": ["REST API", "AWS"], # Optional
    "traceId": "trace-123",
    "proxyUrls": ["http://proxy:port"],  # Optional
}

from crawler.engine import execute_request
result = execute_request(request, base_dir)
print(f"Found {len(result['jobs'])} jobs")
```

### Response Format
```python
{
    "source": "TopCV",
    "keyword": "Python",
    "strategy": "web-scraping",
    "traceId": "trace-123",
    "jobs": [
        {
            "sourceJobId": "topcv-123456",
            "title": "Senior Python Developer",
            "company": "TechCorp",
            "url": "https://www.topcv.vn/viec-lam/...",
            "location": "Ha Noi",
            "salary": "15-20 triệu",
            "description": "We are looking for...",
            "tags": ["python", "rest", "api"],
            "postedAtUtc": "2026-05-10T00:00:00+00:00"
        },
        ...
    ]
}
```

## 🧪 Testing

Run the included test script:
```bash
cd services/crawl-execution-service/src
python test_crawlers.py
```

This tests:
- HTTP fetcher connectivity to each source
- Job extraction capability
- Error handling

## 🛡️ Known Limitations

1. **Cloudflare Pages**: TopCV sometimes uses Cloudflare challenges
   - Solution: `cloudscraper` automatically handles most cases
   - Fallback: Proxy services or manual proxy

2. **Dynamic Content**: Some sites load jobs via JavaScript
   - Current: Static HTML extraction only
   - Future: Consider `playwright` for JS rendering

3. **Rate Limiting**: Sites may throttle repeated requests
   - Solution: Use proxy rotation or throttle requests
   - Respect `robots.txt` directives

4. **HTML Structure Changes**: Site redesigns break selectors
   - Solution: Multi-selector fallback reduces impact
   - Monitoring: Watch for selector failures

## 📈 Performance

- **TopCV**: ~1-2 seconds (with Cloudflare bypass)
- **Careerviet**: ~0.5-1 second
- **Vieclam24h**: ~0.5-1 second
- **Indeed**: ~0.5-1 second

Speeds depend on network conditions and proxy performance.

## 🔐 Security & Ethics

- **robots.txt Compliance**: Checked before crawling (can be disabled)
- **Rate Limiting**: Respectful single-threaded crawling
- **User-Agent**: Rotated realistic browsers
- **Referer**: Set to search page for authenticity

## 🚧 Future Enhancements

1. **JavaScript Rendering**: Add `playwright` for dynamic content
2. **Proxy Pool Management**: Integration with proxy providers
3. **Caching**: Redis/local cache for deduplication
4. **Monitoring**: Enhanced logging and error tracking
5. **Advanced Filters**: Skill matching, company reputation filters
6. **API Integration**: Direct API usage where available
7. **Multi-threading**: Parallel crawling (thread-safe)

## 📝 Changelog

### Latest Version
- ✅ Rewrote `careerviet_source.py` with flexible selector matching
- ✅ Rewrote `vieclam24h_source.py` with multi-selector support
- ✅ Improved `topcv_source.py` with Vietnamese slug encoding
- ✅ Enhanced `http_client.py` with better Cloudflare handling
- ✅ Added `cloudscraper` integration for CF bypass
- ✅ Implemented fallback proxy services
- ✅ Added comprehensive logging for debugging

## 📞 Support

For issues or improvements:
1. Check `http_client.py` for fetcher errors
2. Verify source-specific selectors match current site HTML
3. Enable detailed logging in source parsers
4. Test with `test_crawlers.py` before production deployment

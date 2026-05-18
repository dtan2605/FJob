from pathlib import Path
from crawler.sources.selenium_source import SeleniumSearchSource, SeleniumCrawlLimits
out = Path(r'd:/Project/Hybrid/FJob/temp-topcv-page.html')
source = SeleniumSearchSource(Path(r'd:/Project/Hybrid/FJob/services/crawl-execution-service/src/crawler'))
limits = SeleniumCrawlLimits.from_request({"maxPages":1,"maxJobs":10,"waitTimeoutSeconds":10,"pageLoadTimeoutSeconds":30,"postPageDelayMs":500})
browser = source._create_browser(limits)
try:
    url = source._build_search_url('topcv', 'https://www.topcv.vn', 'python', None, None)
    browser.get(url)
    source._wait_for_page_ready(browser, 'topcv', limits)
    out.write_text(browser.page_source, encoding='utf-8')
    print(out)
finally:
    browser.quit()

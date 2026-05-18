from pathlib import Path
from crawler.sources.careerviet_selenium_source import CareervietSeleniumSource
from crawler.sources.selenium_source import SeleniumCrawlLimits
out = Path(r'd:/Project/Hybrid/FJob/temp-careerviet-intern-page.html')
source = CareervietSeleniumSource(Path(r'd:/Project/Hybrid/FJob/services/crawl-execution-service/src/crawler'))
limits = SeleniumCrawlLimits.from_request({"maxPages":1,"maxJobs":10,"waitTimeoutSeconds":10,"pageLoadTimeoutSeconds":30,"postPageDelayMs":500})
browser = source._create_browser(limits)
try:
    url = source._build_search_url('careerviet', 'https://careerviet.vn', 'intern', None, None)
    print('URL:', url)
    browser.get(url)
    source._wait_for_page_ready(browser, 'careerviet', limits)
    source._perform_search_via_form(browser, 'careerviet', 'intern', None)
    source._wait_for_page_ready(browser, 'careerviet', limits)
    print('CURRENT:', browser.current_url)
    out.write_text(browser.page_source, encoding='utf-8')
    print(out)
finally:
    browser.quit()

# Crawl Execution Service

Phase 1 duoc trien khai duoi dang Python runner khong phu thuoc thu vien ngoai.

## Muc tieu

- nhan crawl request tu orchestration service
- chon source adapter
- uu tien chien luoc API theo SRS
- kiem tra robots/toS policy qua source manifest
- parse du lieu va tra ve jobs da chuan hoa

## Source dau tien

- `TopCV` duoc mo phong bang sample feed local de hoan thien pipeline ingest end-to-end

## File quan trong

- `src/main.py`: entry point
- `src/crawler/engine.py`: source registry
- `src/crawler/sources/topcv_source.py`: source adapter dau tien
- `src/crawler/sample_data`: sample API payload cho Phase 1

## Gioi han co the tu dieu chinh

- Ho tro trong request JSON: `maxPages`, `maxJobs`, `postPageDelayMs`
- Ho tro them cho Selenium: `pageLoadTimeoutSeconds`, `waitTimeoutSeconds`, `browserImplicitWaitSeconds`, `forceStatic`
- Ho tro them cho HTTP/static: `requestTimeoutSeconds`

Vi du request:

```json
{
  "source": "TopCV",
  "keyword": "python intern",
  "traceId": "manual-test",
  "maxPages": 2,
  "maxJobs": 30,
  "postPageDelayMs": 200
}
```

Neu dang di qua duong Selenium va muon ep bo browser de test toc do:

```json
{
  "source": "selenium",
  "website": "topcv",
  "keyword": "python intern",
  "traceId": "manual-test",
  "forceStatic": true
}
```

Co the set bang environment variables:

- `CRAWL_MAX_PAGES`
- `CRAWL_MAX_JOBS`
- `SELENIUM_PAGE_LOAD_TIMEOUT_SECONDS`
- `SELENIUM_WAIT_TIMEOUT_SECONDS`
- `SELENIUM_POST_PAGE_DELAY_MS`
- `SELENIUM_IMPLICIT_WAIT_SECONDS`
- `HTTP_REQUEST_TIMEOUT_SECONDS`
- `HTTP_POST_PAGE_DELAY_MS`

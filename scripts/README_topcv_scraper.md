# TopCV Homepage Scraper

Script Python để cào toàn bộ text từ trang chủ TopCV.vn

## Cách sử dụng

### Chạy trực tiếp với Python (nếu có Python và dependencies)

```bash
cd scripts
python topcv_homepage_scraper.py --save --format json
```

### Chạy trong Docker container

```bash
# Chạy container crawl-execution-service
docker compose -f docker-compose.production.yml exec crawl-execution-service python3 /app/scripts/topcv_homepage_scraper.py --save

# Hoặc copy script vào container và chạy
docker cp scripts/topcv_homepage_scraper.py fjob-crawl-execution-service-1:/app/
docker compose -f docker-compose.production.yml exec crawl-execution-service python3 topcv_homepage_scraper.py --save
```

## Options

- `--save`: Lưu kết quả ra file
- `--format text|json`: Format output (mặc định: text)
- `--quiet`: Không hiển thị text ra console

## Output

### Text format
- Tất cả text đã được clean
- Thông tin structured (headings, meta tags)
- Timestamp và URL

### JSON format
```json
{
  "url": "https://www.topcv.vn",
  "timestamp": "2024-01-01T12:00:00",
  "text": "...",
  "structured_data": {
    "title": "...",
    "headings": {...},
    "links": [...],
    "meta_description": "...",
    "meta_keywords": "..."
  },
  "text_length": 12345
}
```

## Dependencies

- requests
- beautifulsoup4
- lxml
- fake-useragent

(Đã có sẵn trong crawl-execution-service container)
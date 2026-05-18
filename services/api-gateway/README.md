# API Gateway

## Vai tro

Diem vao duy nhat cho frontend va external clients.

## Chuc nang

- route request toi service phu hop
- ap auth/authz o tang edge
- rate limiting, CORS, API versioning
- response aggregation cho cac man hinh can nhieu nguon

## Khong nen chua

- business logic tim kiem sau
- logic crawl
- luu tru domain data

## Phase 2 implementation

- public search endpoint `POST /api/jobs/search`
- forward request sang `Job Search Service`
- giu gateway o muc mong, chua them auth hay aggregation phuc tap

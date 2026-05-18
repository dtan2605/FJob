# Production Readiness Pass

## Muc tieu

Tai lieu nay tom tat nhung nang cap da duoc them sau khi MVP crawl/search/admin/auth da hoan thanh, de he thong tien gan hon toi muc staging-ready.

## Da nang cap

### Backend va platform

- shared observability layer cho correlation id, request metrics, retry, timeout
- metrics endpoint cho cac HTTP services
- rate limiting cho login va public search
- readiness va health checks ro rang hon cho tung service
- persistence nghiep vu da su dung MySQL thay vi file-backed storage

### Frontend Web

- chuyen tu static vanilla JS sang ReactJS SPA
- van giu boundary microservice qua `API Gateway`
- bo sung `GET /ready` de probe `API Gateway`
- bo sung `GET /metrics` de theo doi frontend service
- bo sung response compression, cache policy va security headers

## Tieu chi dat duoc sau pass nay

- co the smoke-test service health va metrics mot cach dong bo
- co the quan sat request volume, failed requests va rate-limited requests
- frontend khong con la static page script-thuan, ma da co state management React de san cho cac user flow lon hon
- service boundary van khong thay doi: frontend -> gateway -> domain services

## Van con la buoc tiep theo

- thay frontend runtime React bang bundler pipeline nhu Vite hoac Next.js neu muon toi uu asset fingerprinting va bundle control
- dua metrics vao he thong scrape/visualize that nhu Prometheus + Grafana
- them automated smoke tests cho `/health`, `/ready`, `/metrics`
- bo sung CI/CD pipeline va environment promotion strategy day du
- can nhac Elastic/OpenSearch that neu search workload tang

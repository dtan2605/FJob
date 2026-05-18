# FJob Microservices Architecture

Repo nay hien da co code MVP den Phase 5 theo roadmap, gom crawl, search, frontend, admin operations, identity va MySQL persistence.
Phase 6 hardening va observability MVP cung da duoc trien khai o muc local/staging-ready.

Phase 1 va Phase 2 MVP hien da duoc scaffold lai de chay pipeline crawl + search toi thieu theo SRS.
Phase 3 MVP hien da co `Frontend Web` de nguoi dung tim kiem job qua giao dien.

## Muc tieu

Phan he thong thanh cac microservice theo tung chuc nang lon de de phat trien doc lap, scale rieng va phan tach ro trach nhiem giua cac doi.

## Cau truc moi

- `services/api-gateway`: diem vao duy nhat cho frontend va client ngoai.
- `services/identity-access-service`: xac thuc, phan quyen, quan ly admin/users.
- `services/job-catalog-service`: nghiep vu du lieu viec lam goc.
- `services/job-search-service`: full-text search, filter, ranking, suggestions.
- `services/crawl-orchestration-service`: lap lich, queue, dieu phoi crawl jobs.
- `services/crawl-execution-service`: thuc thi crawler theo tung nguon.
- `services/job-search-service`: dong bo search index va xu ly full-text search.
- `services/api-gateway`: public search endpoint cho client/frontend.
- `services/admin-operations-service`: dashboard van hanh, crawl history, backlog control.
- `services/frontend-web`: giao dien nguoi dung va giao dien admin.
- `platform/data`: quy hoach DB, search index, cache.
- `platform/messaging`: quy hoach message broker va event contracts.
- `platform/observability`: logging, metrics, tracing, alerting.
- `platform/devops`: CI/CD, environments, release strategy.
- `docs/architecture`: tai lieu kien truc tong the.
- `SRS`: tai lieu SRS goc.

Chi tiet xem tai `docs/architecture/system-overview.md`.
Roadmap trien khai xem tai `docs/architecture/development-roadmap.md`.
Tong hop readiness gan production xem tai `docs/architecture/production-readiness-pass.md`.
Huong dan deployment production xem tai `docs/architecture/production-deployment.md`.

## Phase 1 da co gi

- `Job Catalog Service`: luu job records va crawl run history bang file-backed store.
- `Crawl Orchestration Service`: nhan crawl request, luu queue, retry va dispatch sang Python crawler.
- `Crawl Execution Service`: source adapter dau tien cho `TopCV` bang sample API feed local, co robots/toS gate va tag derivation.

Huong dan chay nhanh xem tai `scripts/run-phase1.ps1`.

## Phase 2 da co gi

- `Job Search Service`: dong bo du lieu tu `Job Catalog Service` theo background sync, luu search index rieng va ho tro keyword search, location filter, tag filter, paging, sort.
- `API Gateway`: mo endpoint `POST /api/jobs/search` de frontend/client goi vao.
- boundary duoc cai thien: `Job Search Service` tu lay du lieu tu `Job Catalog Service`, orchestration khong can biet chi tiet search indexing.
- ban nang cap gan production:
  - search store duoc tach qua abstraction de sau nay thay bang Elasticsearch de dang hon
  - sync co watermark, sync state, endpoint `manual sync` va `full rebuild`
  - query ho tro them salary range, source filter, posted-within-days
  - document search duoc normalize de search khong nhay cam dau tieng Viet/Unicode noise

Huong dan chay nhanh xem tai `scripts/run-phase2.ps1`.

## Phase 3 da co gi

- `Frontend Web` duoc trien khai thanh mot web service rieng, phuc vu ReactJS SPA va goi `API Gateway`.
- co trang search voi:
  - tu khoa
  - location
  - tags
  - source
  - salary range
  - posted-within-days
  - paging va sort
- co URL query sync, loading state, empty state, error state va link mo bai dang goc.

Huong dan chay nhanh xem tai `scripts/run-phase3.ps1`.

## Phase 4 da co gi

- `Admin Operations Service` duoc trien khai thanh mot web service rieng cho van hanh crawl pipeline.
- dashboard hien co:
  - queue summary
  - source controls
  - recent crawl runs
  - queue backlog
  - failed sources
  - recent admin actions
- ho tro thao tac van hanh:
  - manual trigger crawl
  - pause/resume source
  - retry lai crawl request bi fail
- da bo sung cac nang cap de chay local on dinh hon:
  - service doc duoc `appsettings.json` dung theo thu muc project, khong phu thuoc cach launch
  - static admin UI phuc vu duoc ngay ca khi chay tu repo root
  - admin actions co audit log va du lieu van hanh luu trong MySQL
  - cac thao tac nhay cam tren UI co xac nhan truoc khi thuc hien

Huong dan chay nhanh xem tai `scripts/run-phase4.ps1`.

## Phase 5 da co gi

- `Identity Access Service` da duoc trien khai de cap JWT cho admin.
- `Admin Operations Service` da duoc bao ve bang dang nhap admin, role `readonly` va `operator`.
- RBAC hien tai:
  - `readonly`: xem dashboard, crawl history, queue, audit logs
  - `operator`: co them quyen pause/resume source, retry, manual trigger crawl
- `API Gateway` da duoc them JWT validation endpoint toi thieu de kiem tra token qua gateway.
- local seed accounts:
  - `admin.operator / Operator@123`
  - `admin.viewer / Viewer@123`

Huong dan chay nhanh xem tai `scripts/run-phase5.ps1`.

## Phase 6 da co gi

- `platform/observability/FJob.Observability`: shared middleware va utility cho correlation id, request metrics, resilience wrapper.
- cac service HTTP chinh da co:
  - `GET /health`
  - `GET /metrics`
  - correlation id propagation qua response header
  - request timing, error counting, rate-limit counting
- hardening da them:
  - retry + timeout cho outbound HTTP calls giua services
  - rate limiting cho login va public search
  - metrics snapshot de team quan sat request throughput, latency, error rate, rate-limited requests
- `Frontend Web` cung da duoc nang cap:
  - ReactJS runtime SPA thay cho static vanilla JS
  - `GET /ready` de probe ket noi toi `API Gateway`
  - `GET /metrics` cho frontend service
  - response compression, security headers va cache policy cho static assets
- du lieu production/local dev van dung `MySQL` cho persistence nghiep vu, con observability metrics hien tai la in-memory snapshot phuc vu local/staging MVP.
- bo artifact production-ready:
  - `Dockerfile` cho cac service
  - `docker-compose.production.yml` cho full stack
  - `.env.production.example` de quan ly config qua environment variables
  - `scripts/verify-production-readiness.ps1` de smoke-check sau deploy

Huong dan chay nhanh xem tai `scripts/run-phase6.ps1`.

## Database hien tai

He thong hien tai da duoc nang cap tu `file-backed persistence` sang `MySQL` cho cac thanh phan chinh:

- `Identity Access Service`: users va auth audit logs
- `Job Catalog Service`: jobs va crawl run history
- `Crawl Orchestration Service`: queue backlog va source controls
- `Job Search Service`: search documents va sync state
- `Admin Operations Service`: admin audit logs

Mac dinh local dev dang tro vao MySQL tai `127.0.0.1:3307`, database `FJobDb`.
Co 2 cach khoi dong:
- `scripts/start-local-mysql.ps1`
- hoac `docker compose up -d mysql`


Tài khoản MySQL Workbench:
- Host: 127.0.0.1
- Port: 3307
- Database: FJobDb
- User: fjob
- Pass: FJobDb_123!
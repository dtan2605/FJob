# Job Catalog Service

## Vai tro

Noi luu tru du lieu viec lam goc sau khi crawler thu thap.

## Chuc nang

- luu va cap nhat job records
- quan ly source metadata
- chuan hoa tags, company snapshots
- phat su kien khi job thay doi de re-index

## Phase 1 implementation

- ASP.NET Core minimal API
- MySQL persistence
- endpoint tao `crawl run`
- endpoint cap nhat trang thai run
- endpoint upsert batch jobs co idempotency theo `source + sourceJobId`

## Phase 6 hardening

- `GET /metrics` expose request metrics, job count va crawl run count
- correlation id va request timing da duoc bo sung de debug van hanh

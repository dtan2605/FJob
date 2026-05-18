# Admin Operations Service

## Vai tro

Bao phu nghiep vu van hanh he thong.

## Chuc nang

- xem crawl history
- pause/resume sources
- xem backlog va error trend
- dashboard suc khoe he thong o muc nghiep vu
- manual trigger crawl requests
- retry queue item bi fail
- audit log cho thao tac admin
- dang nhap admin bang JWT
- RBAC `readonly` va `operator`

## Local MVP hien tai

- service HTTP chay mac dinh o `http://localhost:5106`
- UI admin phuc vu static qua chinh service nay
- du lieu audit log duoc luu trong MySQL bang `admin_operations_audit_logs`
- service auth noi sang `Identity Access Service` tai `http://localhost:5104`

## API van hanh chinh

- `GET /api/admin/dashboard`
- `GET /api/admin/crawl-runs`
- `GET /api/admin/queue`
- `GET /api/admin/audit-logs`
- `POST /api/admin/crawl-requests`
- `POST /api/admin/crawl-requests/{id}/retry`
- `POST /api/admin/sources/{source}/pause`
- `POST /api/admin/sources/{source}/resume`
- `GET /metrics`

## Phase 6 hardening

- moi admin API van duoc bao ve boi JWT + RBAC
- outbound calls sang cac service khac co retry + timeout wrapper
- metrics endpoint yeu cau role `readonly` hoac `operator`

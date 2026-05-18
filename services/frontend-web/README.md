# Frontend Web

## Vai tro

Bao gom giao dien nguoi dung cuoi va giao dien admin.

## Khu vuc chinh

- job search experience
- job detail
- filter sidebar
- admin operations screens

## Phase 3 implementation

- ASP.NET Core web service phuc vu frontend ReactJS
- trang search goi `API Gateway`
- ho tro URL query sync
- ho tro loading, empty, error state
- hien thi ket qua job va link den bai dang goc

## Phase 6 readiness pass

- `GET /health`, `GET /ready`, `GET /metrics`
- response compression va cache policy cho static assets
- security headers: CSP, frame deny, referrer policy, nosniff
- frontend van giu giao tiep voi backend qua `API Gateway`

## Luu y

Frontend hien tai da duoc chuyen sang ReactJS theo huong runtime SPA de giu service boundary gon va chay duoc ngay.
Neu can production frontend pipeline day du hon nua, buoc tiep theo hop ly la doi sang bundler nhu Vite hoac Next.js nhung van giu contract qua `API Gateway`.

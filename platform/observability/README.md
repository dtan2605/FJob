# Observability Platform

## Thanh phan

- shared middleware cho correlation id
- request metrics snapshot
- resilience wrapper: retry + timeout cho outbound HTTP
- dashboard va alerting conventions

## Doi tuong uu tien giam sat

- search latency
- crawl success rate
- queue backlog
- indexing lag

## Trang thai hien tai

- `FJob.Observability` la thu vien dung chung cho cac ASP.NET services.
- moi service co `GET /metrics` de expose snapshot suc khoe noi bo.
- response deu tra ve header `X-Correlation-Id` de debug cross-service.
- rate-limited requests va server errors duoc dem rieng de team theo doi hardening readiness.

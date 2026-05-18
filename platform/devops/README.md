# DevOps Platform

## Pham vi

- repository strategy
- CI/CD
- environment promotion
- container orchestration
- secrets/config management

## Muc tieu

- tung service co pipeline rieng
- deploy doc lap
- rollback an toan

## Trang thai hien tai

- local dev uu tien chay bang `dotnet run` + `scripts/start-local-mysql.ps1`
- service ports mac dinh:
  - `5100`: api-gateway
  - `5101`: job-catalog-service
  - `5102`: crawl-orchestration-service
  - `5103`: job-search-service
  - `5104`: identity-access-service
  - `5106`: admin-operations-service
- phase 6 da co `health` va `metrics` endpoints de lam nen cho monitoring/staging checks
- chua co CI/CD pipeline day du va autoscaling that; phan nay la buoc tiep theo sau roadmap local MVP
- da co `docker-compose.production.yml`, `.env.production.example` va smoke script `scripts/verify-production-readiness.ps1` de lam deployment artifact co the lap lai hon

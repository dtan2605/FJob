# Production Deployment Guide

## Muc tieu

Tai lieu nay mo ta bo artifact va checklist toi thieu de deploy FJob theo huong production-ready voi Docker Compose.

## Artifact da co

- `docker-compose.production.yml`: full stack deployment cho MySQL, identity, catalog, orchestration, search, gateway, frontend, admin
- `.env.production.example`: bien moi truong mau cho production/staging
- `services/*/Dockerfile`: image build cho tung service
- `scripts/verify-production-readiness.ps1`: smoke script kiem tra health, readiness, metrics va admin dashboard

## Cach chuan bi

1. Copy `.env.production.example` thanh `.env.production`.
2. Thay tat ca mat khau va `JWT_SIGNING_KEY`.
3. Xac nhan cac host port khong trung voi dich vu khac.
4. Xac nhan MySQL volume policy phu hop moi truong.

## Cach deploy

1. `docker compose --env-file .env.production -f docker-compose.production.yml up -d --build`
2. doi MySQL va cac service readiness on dinh
3. chay `.\scripts\verify-production-readiness.ps1` voi URL va admin credentials tuong ung

## Dieu gi da duoc xu ly de gan production hon

- service readiness khong chi check process song ma con probe DB va upstream quan trong
- auth services da co DataProtection key ring noi bo thay vi phu thuoc user profile cua may chay
- orchestration container co san `python3` va crawler script de khong vo pipeline khi containerize
- frontend va admin da la service deploy doc lap, di qua `API Gateway`

## Van con nen lam tiep neu len production that

- dua metrics vao Prometheus/Grafana that
- bo sung TLS, reverse proxy va secret manager
- tach crawl execution thanh worker/service doc lap thay vi process local
- bo sung backup/restore procedure cho MySQL
- bo sung CI/CD pipeline, image tagging, rollback strategy

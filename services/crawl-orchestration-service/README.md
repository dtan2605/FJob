# Crawl Orchestration Service

## Vai tro

Dieu pho tat ca tac vu crawl.

## Chuc nang

- recurring schedules
- manual trigger
- queue, retry, dead-letter
- phan bo workload theo source va keyword

## Phase 1 implementation

- ASP.NET Core minimal API + background worker
- persisted internal queue trong `App_Data`
- manual trigger endpoint
- retry co backoff co ban
- goi Python crawler runner va dong bo ket qua ve Job Catalog Service

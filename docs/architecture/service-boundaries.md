# Service Boundaries

## Service map

| Service | Trach nhiem chinh | Du lieu so huu | Scale theo |
| --- | --- | --- | --- |
| API Gateway | routing, auth delegation, aggregation | khong uu tien so huu domain data | request traffic |
| Identity Access Service | auth, roles, admin accounts | users, roles, refresh tokens | login traffic |
| Job Catalog Service | job master data, source metadata | jobs, companies, tags, sources | ingest volume |
| Job Search Service | full-text search, filters, ranking | search index, synonyms, facets | search QPS |
| Crawl Orchestration Service | schedule, queue, retry, orchestration | crawl plans, crawl runs, retry state | number of jobs |
| Crawl Execution Service | spiders/crawlers per source | ephemeral crawl artifacts | concurrent crawling |
| Admin Operations Service | operation dashboard, controls | audit views, operation settings | admin usage |
| Frontend Web | user/admin UI | UI state | user traffic |

## Tich hop giua service

- Gateway -> Identity Access Service: xac minh token va claims.
- Gateway -> Job Search Service: tim kiem va filter viec lam.
- Gateway -> Admin Operations Service: truy cap dashboard van hanh.
- Crawl Orchestration Service -> Crawl Execution Service: dispatch crawl tasks.
- Crawl Execution Service -> Job Catalog Service: gui ket qua crawl hop le.
- Job Catalog Service -> Job Search Service: phat su kien re-index.
- Admin Operations Service -> Crawl Orchestration Service: manual trigger, pause, resume.

## Phan tach doi phat trien

- Team Platform: gateway, auth, observability, devops.
- Team Data & Search: job catalog, job search, indexing.
- Team Crawling: orchestration, execution, source adapters.
- Team Product: frontend web, admin UX.

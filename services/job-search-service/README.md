# Job Search Service

## Vai tro

Toi uu cho bai toan tim kiem viec lam.

## Chuc nang

- full-text search
- filter theo dia diem, salary, tags, posting age
- ranking va suggestion
- synonym mapping nhu intern/internship/thuc tap

## Phase 2 implementation

- ASP.NET Core minimal API
- MySQL-backed search index
- background sync tu `Job Catalog Service`
- search theo keyword co synonym groups
- filter theo `location`, `tags`
- paging va `recent/relevance` sort

## Gan production hon o diem nao

- `ISearchIndexStore` tach search logic khoi storage engine, de thay the bang Elasticsearch/OpenSearch sau nay
- sync state co `watermark` va `last error` de giam nguy co mat du lieu khi dong bo
- co endpoint `manual sync` va `full rebuild` de doi van hanh sua index ma khong can can thiep file/DB tay
- search document co normalized fields va salary ranges de phuc vu query thuc te hon

## Phase 6 hardening

- `GET /metrics` expose request metrics va sync state
- outbound sync call sang `Job Catalog Service` co retry + timeout wrapper
- search logic tiep tuc tach khoi storage qua `ISearchIndexStore`, giu duong nang cap len Elasticsearch/OpenSearch sau nay

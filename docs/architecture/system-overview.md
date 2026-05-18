# System Overview

## Dinh huong tong the

FJob duoc chia thanh cac microservice theo domain lon thay vi chia theo technical layer. Moi service so huu trach nhiem rieng, co kha nang trien khai doc lap va scale doc lap.

## Danh sach microservice

### 1. API Gateway

- Lam entry point cho frontend va cac client ben ngoai.
- Xu ly routing, rate limiting, auth delegation, response aggregation.
- Khong chua business logic nang.

### 2. Identity Access Service

- Dang nhap, JWT/OAuth, roles, permissions.
- Quan ly tai khoan admin va auditing truy cap.
- Cung cap token/claims cho Gateway va cac service can xac thuc.

### 3. Job Catalog Service

- Luu tru du lieu viec lam goc sau khi crawl.
- Quan ly schema job, company snapshot, tags, source metadata.
- La nguon su that chinh cho du lieu viec lam.

### 4. Job Search Service

- Quan ly search index va query phuc tap.
- Tim kiem full-text, filter, faceting, ranking.
- Toi uu cho toc do doc va trai nghiem search.

### 5. Crawl Orchestration Service

- Lap lich crawl, tao queue, retry policy, dead-letter policy.
- Quan ly crawl definitions theo source, keyword, cadence.
- Dieu phoi jobs toi Crawl Execution Service.

### 6. Crawl Execution Service

- Chua cac crawler/spider theo tung nguon.
- Uu tien API crawl, fallback HTML parse, Playwright neu can.
- Day ket qua crawl vao Job Catalog Service hoac messaging pipeline.

### 7. Admin Operations Service

- Giao dien/logic quan tri van hanh.
- Theo doi lich su crawl, manual trigger, pause/resume source.
- Quan ly tinh trang backlog, throughput, error trends.

### 8. Frontend Web

- Website tim kiem viec lam cho end-user.
- Khu vuc admin cho van hanh he thong.
- Giao tiep voi API Gateway thay vi goi truc tiep tung service.

## Nen tang dung chung

### Data Platform

- OLTP database cho Job Catalog va Identity.
- Search index rieng cho Job Search.
- Redis cho cache va phan tan state nhe.

### Messaging Platform

- Broker cho async workflow giua orchestration, crawler, indexing.
- Event-driven cho cac tac vu dai va retry.

### Observability Platform

- Log tap trung, metrics, traces, dashboards, alerting.
- Moi request va moi crawl run deu phai co trace id.

### DevOps Platform

- Docker/Kubernetes, CI/CD, environment strategy, secrets management.

## Luong du lieu cap cao

1. Nguoi dung tim viec qua Frontend Web.
2. Frontend goi API Gateway.
3. Gateway goi Job Search Service de lay ket qua tim kiem.
4. Job Search Service doc search index va tra ket qua.
5. He thong crawl duoc Crawl Orchestration Service lap lich.
6. Crawl Execution Service lay task, thu thap du lieu, gui ket qua.
7. Job Catalog Service luu du lieu goc.
8. Job Search Service dong bo lai chi muc tim kiem.

## Nguyen tac phan tach

- Tach service theo business capability, khong theo framework.
- Moi service so huu data va lifecycle rieng.
- Dong bo qua API hoac event, khong share DB truc tiep.
- Uu tien eventual consistency cho crawl va indexing pipeline.

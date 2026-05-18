# Development Roadmap

## Muc tieu roadmap

Roadmap nay tra loi 3 cau hoi:

1. Nen xay phan nao truoc de co gia tri som nhat.
2. Moi phase can hoan thanh den muc nao moi duoc chuyen phase.
3. Tieu chi danh gia thanh cong cua tung phan he la gi.

## Nguyen tac uu tien

- Uu tien xay duong di ngan nhat tao ra gia tri cot loi: crawl duoc, luu duoc, tim duoc.
- Uu tien vertical slice truoc horizontal completeness.
- Uu tien 1 nguon crawl chay that tot truoc khi mo rong nhieu nguon.
- Uu tien admin van hanh toi thieu truoc khi toi uu hoa giao dien dep.
- Auth day du chi nen hoan thien som cho admin, chua can lam nang cho end-user ngay phase dau.

## Thu tu trien khai de xuat

1. Platform foundation
2. Job Catalog Service
3. Crawl Orchestration Service
4. Crawl Execution Service
5. Job Search Service
6. API Gateway
7. Frontend Web
8. Admin Operations Service
9. Identity Access Service
10. Observability, hardening, scale-out

## Phase 0: Architecture Foundation

### Muc tieu

Dat nen tang ky thuat va quy uoc phat trien de cac doi co the code song song ma khong va cham nhau.

### Nen lam truoc

- chot stack cong nghe cho tung service
- chot giao tiep giua services: sync API gi, async event gi
- chot convention repo, branching, environments
- chot cach dat ten event, API path, correlation id
- chot quy tac so huu du lieu cho tung service

### Dau ra can co

- architecture decision records
- service contracts draft
- event flow draft
- environment map: local, dev, staging, prod
- coding standards va delivery standards

### Tieu chi hoan thanh

- moi service co owner ro rang
- moi service biet input/output cua minh
- khong con tranh cai lon ve boundary
- team co the bat dau code ma khong phai doi nhau ra quyet dinh nen tang

## Phase 1: Core Data Ingestion MVP

### Muc tieu

Tao duoc pipeline toi thieu de lay job tu 1 nguon, luu vao he thong, co log va co trang thai run.

### Nen trien khai

- Job Catalog Service
- Crawl Orchestration Service
- Crawl Execution Service
- Messaging Platform toi thieu
- Data Platform toi thieu

### Pham vi nen gioi han

- chi 1 nguon crawl dau tien
- chi 1 chien luoc crawl on dinh nhat
- chi nhung field bat buoc cua job
- chua can UI day du

### Dau ra can co

- tao duoc crawl request
- crawl worker xu ly duoc request
- validate va upsert duoc job vao Job Catalog
- luu duoc crawl run history co status
- retry duoc loi tam thoi

### Tieu chi hoan thanh

- crawl thanh cong toi thieu 1 nguon that
- du lieu job luu duoc va khong bi duplicate nghiem trong
- co crawl run history de dieu tra loi
- pipeline co the chay lap lai ma khong lam hong data

### Tieu chi quality

- job schema ro rang, co version neu can
- source adapter duoc tach rieng
- co idempotency cho ingest
- co unit test cho parser/mapper quan trong

## Phase 2: Search MVP

### Muc tieu

Cho phep nguoi dung tim kiem duoc du lieu da crawl theo keyword va mot so filter co ban.

### Nen trien khai

- Job Search Service
- dong bo Job Catalog -> Job Search
- API Gateway phan search toi thieu

### Pham vi nen gioi han

- full-text keyword
- filter dia diem
- filter tag
- phan trang
- sort theo do moi

### Dau ra can co

- pipeline re-index tu Job Catalog sang Search
- search endpoint hoat dong
- co du lieu searchable that
- synonyms co ban cho intern, internship, thuc tap

### Tieu chi hoan thanh

- tu 1 keyword co tra ket qua phu hop
- du lieu moi crawl xong xuat hien tren search trong thoi gian chap nhan duoc
- filter va paging chay dung
- ket qua search khong phu thuoc truc tiep vao OLTP DB

### Tieu chi quality

- index mapping co chu dich
- co retry/repair cho indexing failures
- co test cho query cases chinh
- response time dat muc chap nhan duoc tren dataset MVP

## Phase 3: User-Facing Product MVP

### Muc tieu

Co san mot trai nghiem dau cuoi cho nguoi dung: mo web, tim job, xem danh sach, mo link goc.

### Nen trien khai

- Frontend Web cho user flow
- API Gateway cho public search flow

### Pham vi nen gioi han

- trang search
- filter co ban
- job list
- job detail lite
- link sang bai dang goc

### Dau ra can co

- frontend goi Gateway thanh cong
- co empty state, loading state, error state
- URL query co the share lai

### Tieu chi hoan thanh

- user co the tim duoc viec tren web ma khong can tool noi bo
- search flow hoan chinh tren mobile va desktop
- loi backend duoc frontend hien thi gon gang

### Tieu chi quality

- page speed chap nhan duoc
- giao dien khong vo tren mobile
- tracking user event co ban neu team can do luong

## Phase 4: Operations MVP

### Muc tieu

Van hanh duoc he thong ma khong phai can thiep bang tay qua DB hay queue tools.

### Nen trien khai

- Admin Operations Service
- dashboard van hanh toi thieu
- manual trigger crawl
- pause/resume source

### Dau ra can co

- xem duoc crawl history
- xem duoc source nao dang loi
- trigger lai duoc job crawl
- theo doi backlog co ban

### Tieu chi hoan thanh

- team van hanh xu ly duoc 80% tinh huong thong thuong tu dashboard
- khong can ssh vao server hay sua truc tiep DB cho cac case co ban

### Tieu chi quality

- action quan tri co audit log
- thao tac nguy hiem co xac nhan
- phan quyen admin ro rang

## Phase 5: Security and Access Control

### Muc tieu

Bo sung xac thuc va phan quyen du muc de bao ve he thong, nhat la khu vuc admin.

### Nen trien khai

- Identity Access Service
- Gateway auth integration
- admin RBAC

### Nen uu tien truoc

- admin auth truoc end-user auth
- service-to-service trust sau do moi toi user profile phuc tap

### Tieu chi hoan thanh

- admin phai dang nhap moi vao duoc dashboard
- role phan biet readonly va operator
- token validation on dinh qua Gateway

### Tieu chi quality

- secrets duoc quan ly an toan
- refresh/revoke token co design ro
- audit login, logout, failed access

## Phase 6: Hardening and Scale

### Muc tieu

Chuyen tu MVP sang he thong co the van hanh on dinh va mo rong.

### Nen trien khai

- Observability Platform day du
- caching
- rate limiting
- circuit breaker, retry, timeout policy
- CI/CD hoan chinh
- containerization, autoscaling, environment promotion

### Tieu chi hoan thanh

- co dashboard cho search latency, crawl success rate, queue backlog, indexing lag
- co alert khi crawl loi lien tuc hoac search latency tang cao
- deploy co the lap lai, rollback duoc
- scale them worker crawl ma khong doi code domain

### Tieu chi quality

- SLO/SLA noi bo duoc dinh nghia
- runbook van hanh co san
- incident logging va postmortem flow ro rang

## Thu tu chi tiet theo service

### Nen code dau tien

#### Job Catalog Service

Vi day la noi luu truth data. Neu chua co service nay, crawler se khong co dich den on dinh.

#### Crawl Orchestration Service

Vi can co noi tao request, retry va lifecycle run. Neu bo qua, ve sau se rat kho van hanh.

#### Crawl Execution Service

Vi day la nguon tao gia tri du lieu ban dau. Nhung chi nen bat dau voi 1 source.

### Nen code tiep theo

#### Job Search Service

Sau khi da co data goc on dinh, luc nay search moi co y nghia va de do chinh xac hon.

#### API Gateway

Nên dung som, nhung khong can lam qua phuc tap o giai doan dau. Truoc het chi can public mot vai route chinh.

#### Frontend Web

Nen xay sau khi search API da on dinh toi thieu, tranh frontend bi block vi contract thay doi lien tuc.

### Nen hoan thien sau

#### Admin Operations Service

Can co ngay sau MVP ingestion/search de doi van hanh do bot dau hoi ky thuat.

#### Identity Access Service

Nen dua vao truoc khi mo rong team van hanh hoac dua staging cho nhieu nguoi dung noi bo.

## Definition of Done cap he thong

Mot phase duoc xem la xong khi:

- co flow chay that end-to-end
- co test cho nhung logic quan trong
- co logging de debug
- co tai lieu contract va cach van hanh toi thieu
- co chi so de biet no dang khoe hay hong
- khong can thao tac tay nguy hiem de van hanh binh thuong

## Chi so can theo doi theo tung giai doan

### Phase 1

- so crawl run thanh cong / that bai
- thoi gian crawl trung binh
- ti le duplicate jobs

### Phase 2

- search latency p95
- indexing lag
- search zero-result rate

### Phase 3

- page load time
- search-to-click conversion co ban
- frontend error rate

### Phase 4 tro di

- admin action success rate
- backlog size
- mean time to detect va mean time to recover

## De xuat cach chia sprint

- Sprint 1-2: Phase 0 + Phase 1
- Sprint 3-4: Phase 2
- Sprint 5: Phase 3
- Sprint 6: Phase 4
- Sprint 7: Phase 5
- Sprint 8 tro di: Phase 6 va mo rong them sources

## Khuyen nghi cuoi

- Khong nen lam nhieu nguon crawl truoc khi 1 nguon dau tien that su on dinh.
- Khong nen lam UI admin dep truoc khi da co du lieu van hanh that.
- Khong nen dua ElasticSearch vao qua som neu ingest schema chua on dinh, nhung cung khong nen de search dua tren DB qua lau.
- Khong nen bat dau bang auth qua day du cho end-user neu san pham cot loi van chua xong search + crawl.

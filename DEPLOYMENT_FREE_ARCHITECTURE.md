# FJob deployment free-friendly

## Kết luận nhanh

- `Vercel + Supabase + Ollama` không phải là tổ hợp phù hợp để chạy toàn bộ hệ thống hiện tại.
- Phần phù hợp đưa lên `Vercel` là frontend Angular ở chế độ `browser/static`.
- `Supabase` phù hợp cho `Auth`, `Storage`, và có thể là `Postgres` nếu sau này bạn migrate khỏi MySQL.
- `Ollama`, crawler Selenium, RabbitMQ, Redis, và các .NET API nên đặt trên một `VM/container host` riêng.

## Vì sao

Hệ thống hiện tại có các thành phần:

- frontend Angular
- api-gateway .NET
- job-search-service
- crawl-orchestration-service
- crawl-execution-service dùng Selenium
- MySQL
- Redis
- RabbitMQ
- AI advisor qua Ollama

Kiến trúc này cần tiến trình chạy nền lâu dài, tài nguyên ổn định, và nhiều service phụ trợ. Nó không hợp với mô hình serverless/edge-only.

## Kiến trúc khuyên dùng

### Phương án tốt nhất để deploy free ổn định

- `Vercel`
  - deploy frontend Angular tĩnh
  - dùng file `services/frontend-web/src/FJob.FrontendWeb/vercel.json`
- `Oracle Cloud Always Free VM`
  - chạy `api-gateway`
  - chạy `job-search-service`
  - chạy `crawl-orchestration-service`
  - chạy `crawl-execution-service`
  - chạy `mysql`, `redis`, `rabbitmq`
  - chạy `ollama`

### Phương án hybrid với Supabase

- `Vercel`
  - frontend Angular tĩnh
- `Supabase`
  - Auth
  - Storage
  - tùy chọn Postgres cho dữ liệu mới
- `Oracle Cloud Always Free VM`
  - crawler
  - search service
  - api-gateway
  - ollama

Lưu ý: code hiện tại đang dùng `MySQL` trong nhiều service, nên `Supabase Postgres` chưa phải thay thế drop-in ngay bây giờ.

## Trạng thái hiện tại trong repo

### Ollama

`api-gateway` đã hỗ trợ gọi Ollama OpenAI-compatible API qua các biến môi trường:

- `AiAdvisor__UseOllama`
- `AiAdvisor__BaseUrl`
- `AiAdvisor__Model`
- `AiAdvisor__Temperature`
- `AiAdvisor__TimeoutSeconds`

Nếu Ollama không sẵn sàng, hệ thống tự fallback về bộ advisor nội bộ.

### Docker

`docker-compose.production.yml` đã có sẵn env cho Ollama:

- `AI_ADVISOR_USE_OLLAMA`
- `AI_ADVISOR_BASE_URL`
- `AI_ADVISOR_MODEL`
- `AI_ADVISOR_TEMPERATURE`
- `AI_ADVISOR_TIMEOUT_SECONDS`

Ví dụ:

```env
AI_ADVISOR_USE_OLLAMA=true
AI_ADVISOR_BASE_URL=http://host.docker.internal:11434
AI_ADVISOR_MODEL=qwen2.5:3b
AI_ADVISOR_TEMPERATURE=0.2
AI_ADVISOR_TIMEOUT_SECONDS=45
```

## Deploy frontend lên Vercel

Root deploy nên là:

`services/frontend-web/src/FJob.FrontendWeb`

Biến môi trường cần có:

```env
PUBLIC_API_GATEWAY_BASE_URL=https://your-api-domain.example.com
```

Nếu cần build local trước:

```powershell
npm install
npm run build
```

Vercel sẽ publish từ:

`dist/fjob-frontend-web/browser`

## Deploy backend lên VM

Trên VM, cách đơn giản nhất là chạy:

```powershell
docker compose -f docker-compose.production.yml up -d --build
```

Nếu chạy Ollama cùng VM:

- cài Ollama trực tiếp trên máy host
- pull model nhẹ như `qwen2.5:3b`
- expose `11434`
- để `api-gateway` gọi qua `AI_ADVISOR_BASE_URL`

## Gợi ý model Ollama

Cho máy free-tier CPU:

- `qwen2.5:3b`
- `llama3.2:3b`

Không nên dùng model quá lớn nếu bạn còn chạy Selenium crawl cùng máy.

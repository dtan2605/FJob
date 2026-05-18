OCR service (Tesseract)

This service provides a simple HTTP endpoint `/api/ocr/parse` that accepts a file upload
and returns extracted text using Tesseract OCR (and `pdf2image` for PDFs).

Build and run with Docker Compose (already wired in docker-compose.production.yml).

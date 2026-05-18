FROM python:3.13-slim
WORKDIR /app
COPY services/crawl-execution-service/requirements.txt /app/
RUN pip install --no-cache-dir -r requirements.txt
COPY services/crawl-execution-service/src /app/src
ENTRYPOINT ["python", "/app/src/main.py"]

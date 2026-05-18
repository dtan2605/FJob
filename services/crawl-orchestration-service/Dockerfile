FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish services/crawl-orchestration-service/src/FJob.CrawlOrchestrationService/FJob.CrawlOrchestrationService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip wget gnupg \
    && rm -rf /var/lib/apt/lists/*
RUN wget -q -O - https://dl.google.com/linux/linux_signing_key.pub | gpg --dearmour -o /usr/share/keyrings/google-linux-signing-keyring.gpg
RUN echo "deb [arch=amd64 signed-by=/usr/share/keyrings/google-linux-signing-keyring.gpg] http://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list
RUN apt-get update \
    && apt-get install -y --no-install-recommends google-chrome-stable \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY services/crawl-execution-service/src /opt/fjob/crawl-execution-service/src
COPY services/crawl-execution-service/requirements.txt /opt/fjob/crawl-execution-service/requirements.txt
RUN python3 -m pip install --no-cache-dir --break-system-packages --retries 5 --default-timeout=1000 -r /opt/fjob/crawl-execution-service/requirements.txt
RUN mkdir -p /app/App_Data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FJob.CrawlOrchestrationService.dll"]

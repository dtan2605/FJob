import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';

const port = Number(process.env['PORT'] ?? 8080);
const appName = process.env['APP_NAME'] ?? 'FJob Tìm việc';
const publicApiGatewayBaseUrl = process.env['PUBLIC_API_GATEWAY_BASE_URL'] ?? 'http://localhost:5100';
const internalApiGatewayBaseUrl = process.env['INTERNAL_API_GATEWAY_BASE_URL'] ?? 'http://api-gateway:8080';
const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

app.disable('x-powered-by');

app.use((req, res, next) => {
  const connectSrc = ['\'self\'', publicApiGatewayBaseUrl].join(' ');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('X-Frame-Options', 'DENY');
  res.setHeader('Referrer-Policy', 'strict-origin-when-cross-origin');
  res.setHeader('Permissions-Policy', 'camera=(), microphone=(), geolocation=()');
  res.setHeader(
    'Content-Security-Policy',
    [
      "default-src 'self'",
      "base-uri 'self'",
      "object-src 'none'",
      "frame-ancestors 'none'",
      "img-src 'self' data:",
      "style-src 'self' 'unsafe-inline'",
      "font-src 'self'",
      "script-src 'self'",
      `connect-src ${connectSrc}`,
      "form-action 'self'"
    ].join('; ')
  );

  next();
});

app.get('/health', (_req, res) => {
  res.json({
    service: 'frontend-web',
    status: 'healthy',
    time: new Date().toISOString(),
  });
});

app.get('/ready', async (_req, res) => {
  try {
    const response = await fetch(`${internalApiGatewayBaseUrl}/health`);
    if (!response.ok) {
      throw new Error(`Gateway trả về ${response.status}.`);
    }

    res.json({
      service: 'frontend-web',
      ready: true,
      dependencies: { apiGateway: 'reachable' },
      time: new Date().toISOString(),
    });
  } catch (error) {
    res.status(503).json({
      service: 'frontend-web',
      ready: false,
      dependencies: { apiGateway: 'unreachable' },
      error: error instanceof Error ? error.message : 'Gateway không phản hồi.',
      time: new Date().toISOString(),
    });
  }
});

app.get('/metrics', (_req, res) => {
  res.json({
    service: 'frontend-web',
    app: appName,
    renderingMode: 'angular-ssr',
    apiGatewayBaseUrl: publicApiGatewayBaseUrl,
  });
});

app.get('/api/config', (_req, res) => {
  res.setHeader('Cache-Control', 'no-store, no-cache, must-revalidate');
  res.json({
    apiGatewayBaseUrl: publicApiGatewayBaseUrl,
    appName,
    renderingMode: 'angular-ssr',
  });
});

app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) => (response ? writeResponseToNodeResponse(response, res) : next()))
    .catch(next);
});

if (isMainModule(import.meta.url) || process.env['pm_id']) {
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Frontend SSR server listening on http://localhost:${port}`);
  });
}

export const reqHandler = createNodeRequestHandler(app);

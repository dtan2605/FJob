FROM node:24-bookworm-slim AS frontend-build
WORKDIR /src/services/frontend-web/src/FJob.FrontendWeb
COPY services/frontend-web/src/FJob.FrontendWeb/package.json ./
COPY services/frontend-web/src/FJob.FrontendWeb/package-lock.json ./
COPY services/frontend-web/src/FJob.FrontendWeb/angular.json ./
COPY services/frontend-web/src/FJob.FrontendWeb/tsconfig.json ./
COPY services/frontend-web/src/FJob.FrontendWeb/tsconfig.app.json ./
COPY services/frontend-web/src/FJob.FrontendWeb/ClientApp ./ClientApp
RUN npm ci
RUN npm run build

FROM node:24-bookworm-slim
WORKDIR /app
COPY --from=frontend-build /src/services/frontend-web/src/FJob.FrontendWeb/package.json ./
COPY --from=frontend-build /src/services/frontend-web/src/FJob.FrontendWeb/package-lock.json ./
COPY --from=frontend-build /src/services/frontend-web/src/FJob.FrontendWeb/node_modules ./node_modules
COPY --from=frontend-build /src/services/frontend-web/src/FJob.FrontendWeb/dist ./dist
ENV NODE_ENV=production
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["node", "dist/fjob-frontend-web/server/server.mjs"]

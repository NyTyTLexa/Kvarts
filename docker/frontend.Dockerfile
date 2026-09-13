# Контекст — корень репозитория.
FROM node:22-bookworm-slim AS build
WORKDIR /app

# npm в Docker часто рвёт TLS на IPv6 (ECONNRESET). Сначала A-записи, ретраи.
ENV NODE_OPTIONS=--dns-result-order=ipv4first
ARG NPM_REGISTRY=https://registry.npmjs.org
RUN npm config set registry "${NPM_REGISTRY}" \
 && npm config set fetch-retries 8 \
 && npm config set fetch-retry-mintimeout 20000 \
 && npm config set fetch-retry-maxtimeout 120000 \
 && npm config set fetch-timeout 300000

COPY frontend/package.json frontend/package-lock.json ./
RUN set -eux; \
    i=0; \
    until [ "$i" -ge 5 ]; do \
      npm ci --no-audit --no-fund && break; \
      i=$((i+1)); \
      echo "npm ci failed ($i/5), retry in 20s"; \
      sleep 20; \
    done; \
    test -d node_modules

COPY frontend/ .
ARG VITE_KEYCLOAK_URL=http://localhost:8088/realms/procurement
ENV VITE_KEYCLOAK_URL=${VITE_KEYCLOAK_URL}
# `npm run build` = `tsc -b && vite build`. Код 2 почти всегда ошибки TypeScript — смотри `error TS` выше, не Gordon.
RUN npm run build

FROM nginx:1.27-alpine
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80

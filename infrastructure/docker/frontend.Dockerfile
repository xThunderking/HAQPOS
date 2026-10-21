# syntax=docker/dockerfile:1
FROM ghcr.io/voidzero-dev/vite-plus:1.3.0 AS build
WORKDIR /app
COPY --chown=vp:vp frontend/haqpos-web/package.json frontend/haqpos-web/pnpm-lock.yaml frontend/haqpos-web/pnpm-workspace.yaml frontend/haqpos-web/.node-version frontend/haqpos-web/.npmrc ./
RUN vp install --frozen-lockfile
COPY --chown=vp:vp frontend/haqpos-web/ ./
RUN vp check && vp test run && vp build

FROM nginx:1.30.2-alpine AS runtime
COPY infrastructure/nginx/haqpos.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80 443
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 CMD wget -q --spider http://127.0.0.1/healthz || exit 1

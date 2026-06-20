# syntax=docker/dockerfile:1
# MIGRATION: Multi-stage build for the Angular 19 SPA served by nginx (Linux/Alpine).
# Build context is the repository root (see docker/docker-compose.yml).

# ---- build stage ----
# node:20-alpine provides the latest Node 20.x LTS (>= 20.20.2), satisfying the
# Angular 19 engine requirement and the toolchain version policy.
FROM node:20-alpine AS build
WORKDIR /app

COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci || npm install

COPY frontend/ ./
RUN npm run build -- --configuration production

# ---- runtime stage ----
FROM nginx:alpine AS final
RUN rm -f /etc/nginx/conf.d/default.conf
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/dnn-migration/browser /usr/share/nginx/html
EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:80/ || exit 1

CMD ["nginx", "-g", "daemon off;"]

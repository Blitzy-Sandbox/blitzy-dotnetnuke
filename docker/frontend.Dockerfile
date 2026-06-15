# syntax=docker/dockerfile:1
#
# DnnMigration Frontend — multi-stage Linux build for the Angular 19 SPA, served by nginx.
# Build context is the repository root (see docker/docker-compose.yml).
#
# NOTE (setup): Container image builds require the frontend source (main.ts, app components,
# index.html, styles.scss) authored by file-implementation agents AND a Linux-capable Docker
# daemon. They cannot be built on the Windows-container CI host used during environment setup.

# ---- Build stage ----
# Node 20 satisfies the project's runtime floor (>= 20.20.2) and Angular 19's engine range.
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies first (use the lockfile when present for reproducible installs).
COPY frontend/package.json frontend/package-lock.json* ./
RUN if [ -f package-lock.json ]; then npm ci; else npm install; fi

# Copy the rest of the workspace and produce an optimized production build.
COPY frontend/ ./
RUN npm run build -- --configuration production

# ---- Runtime stage ----
FROM nginx:alpine AS runtime

# SPA routing + /api reverse proxy + Content-Security-Policy.
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# Angular's "application" builder emits browser assets under dist/<project>/browser.
COPY --from=build /app/dist/dnn-migration-frontend/browser /usr/share/nginx/html

EXPOSE 80

# Validation Gate 7: the SPA root must return HTTP 200.
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:80/ >/dev/null 2>&1 || exit 1

CMD ["nginx", "-g", "daemon off;"]

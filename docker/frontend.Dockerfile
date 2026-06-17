# syntax=docker/dockerfile:1
# =============================================================================
# frontend.Dockerfile — Angular 19 SPA build -> nginx:alpine static server
# Build context: repository root (compose `context: ..`; or `docker build -f docker/frontend.Dockerfile .`)
# =============================================================================

# ---- Stage 1: build the Angular production bundle ---------------------------
# Node 22 (active LTS) — Node 20 reached end-of-life, and Angular 19 supports the
# Node ^22 line, so the build image tracks a supported, Angular-19-compatible LTS.
FROM node:22-alpine AS build
WORKDIR /app

# Copy manifest(s) first for dependency-layer caching.
# (package-lock.json is optional; npm ci falls back to npm install when absent.)
COPY frontend/package*.json ./
RUN npm ci || npm install

# Copy the rest of the workspace and build for production.
COPY frontend/ ./
RUN npm run build -- --configuration production

# ---- Stage 2: serve with nginx (unprivileged / non-root) --------------------
# nginxinc/nginx-unprivileged runs as the unprivileged "nginx" user (uid 101) and
# listens on 8080 by default — a non-privileged port that needs no root. This
# satisfies the non-root container requirement: the master process never runs as
# root, so the stock "nginx:alpine" root-master model is eliminated. All paths
# nginx must write (client/proxy temp dirs, the /tmp/nginx.pid file) are already
# owned by uid 101 in this base image.
FROM nginxinc/nginx-unprivileged:alpine AS final

# Install curl for the container HEALTHCHECK (busybox lacks a reliable curl).
# Switch to root ONLY for the package install + config/asset copy, then drop back
# to the unprivileged "nginx" user for the container runtime.
USER root
RUN apk add --no-cache curl

# Replace the stock server block with the SPA + reverse-proxy config.
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# Copy the compiled SPA (application builder emits to dist/<project>/browser).
# Files are world-readable, so the unprivileged nginx user serves them read-only.
COPY --from=build /app/dist/dnn-migration/browser /usr/share/nginx/html

# Drop privileges: the container runs as the unprivileged "nginx" user (uid 101).
USER nginx

# Unprivileged nginx listens on 8080 (see nginx.conf); 80 would require root.
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

CMD ["nginx", "-g", "daemon off;"]

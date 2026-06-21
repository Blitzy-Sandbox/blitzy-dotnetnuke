# syntax=docker/dockerfile:1
# =============================================================================
# frontend.Dockerfile — Angular 19 SPA build -> nginx:alpine static server
# Build context: repository root (compose `context: ..`; or `docker build -f docker/frontend.Dockerfile .`)
# =============================================================================

# ---- Stage 1: build the Angular production bundle ---------------------------
# MIGRATION (Finding CP5 MAJOR — base-image currency): build on the supported Node 22 Alpine line.
# Node 20 has reached end-of-life, and Angular 19.2 supports Node 22; pinning the build stage to a
# current, supported base removes the EOL/known-vulnerable build image. The runtime stage below remains
# nginx:alpine, so this changes only the build toolchain image, not the served artifact.
FROM node:22-alpine AS build
WORKDIR /app

# Copy manifest(s) first for dependency-layer caching.
# (package-lock.json is optional; npm ci falls back to npm install when absent.)
COPY frontend/package*.json ./
RUN npm ci || npm install

# Copy the rest of the workspace and build for production.
COPY frontend/ ./
RUN npm run build -- --configuration production

# ---- Stage 2: serve with nginx ----------------------------------------------
FROM nginx:alpine AS final

# curl for the container HEALTHCHECK (busybox lacks a reliable curl).
RUN apk add --no-cache curl

# Replace the stock server block with the SPA + reverse-proxy config.
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# Copy the compiled SPA (application builder emits to dist/<project>/browser).
COPY --from=build /app/dist/dnn-migration/browser /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]

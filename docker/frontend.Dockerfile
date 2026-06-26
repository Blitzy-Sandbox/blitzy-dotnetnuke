# MIGRATION: Multi-stage Dockerfile for the Angular 19 SPA, replacing the legacy DotNetNuke Web Forms
# admin UI (Website/admin/**) and its Windows/IIS hosting (Website/release.config). Build context =
# repository ROOT, so COPY source paths are relative to the repo root (frontend/..., docker/...).
# Usage:
#   docker build -f docker/frontend.Dockerfile -t dnnmigration-frontend .
#   (compose) services.frontend.build = { context: .., dockerfile: docker/frontend.Dockerfile }

# ---------- Stage 1: build the SPA ----------
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies first for layer caching. The glob copies package.json AND package-lock.json,
# both of which `npm ci` requires for a reproducible, lockfile-pinned install (Gate 3).
# MIGRATION: [CP4 review — Reproducibility / Build Discipline] Uses `npm ci` unconditionally, with no
# fallback path: if package-lock.json is missing or out of sync with package.json the build MUST fail
# loudly rather than silently floating dependency resolution, which would make the image
# non-reproducible and defeat Gate 3's reproducible-install requirement.
COPY frontend/package*.json ./
RUN npm ci

# Copy the rest of the Angular workspace and produce the production bundle (Gate 3 command).
COPY frontend/ ./
RUN npm run build -- --configuration production

# ---------- Stage 2: serve via nginx ----------
FROM nginx:alpine AS final

# Replace the stock welcome page and install the SPA server config
# (history-API fallback, /api reverse proxy, CSP + security headers).
RUN rm -rf /usr/share/nginx/html/*
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# The Angular 19 application builder emits to dist/<project>/browser; angular.json sets
# outputPath = dist/dnn-migration-frontend, so the static files live in the /browser subfolder.
# (This exact path is the coordination contract with frontend/angular.json.)
COPY --from=build /app/dist/dnn-migration-frontend/browser /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]

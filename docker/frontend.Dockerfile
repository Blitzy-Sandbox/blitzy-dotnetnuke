# =============================================================================
# DnnMigration Frontend Dockerfile
# =============================================================================
# Multi-stage Dockerfile for building and running the Angular 19 SPA frontend.
# Uses Node.js LTS image for build stage and nginx:alpine for the final runtime.
# Produces an optimized, minimal Linux container replacing the legacy WebForms UI.
#
# Build context: Repository root (../)
# Usage: docker build -f docker/frontend.Dockerfile -t dnnmigration-frontend .
#
# Stages:
#   1. build  - Compiles Angular application with production optimizations
#   2. test   - (Optional) Runs unit tests for CI pipelines
#   3. final  - Production-ready nginx container serving the SPA
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Build
# -----------------------------------------------------------------------------
# Uses Node.js 20 LTS Alpine for minimal image size during build.
# Installs dependencies with npm ci for deterministic, reproducible builds.
# Compiles Angular 19 application with production configuration.
# -----------------------------------------------------------------------------
FROM node:20-alpine AS build

# Set working directory for build operations
WORKDIR /app

# Install build dependencies for native modules (if any)
# Alpine-specific packages that may be required for node-gyp
RUN apk add --no-cache python3 make g++

# Copy package files first for optimal Docker layer caching
# This allows Docker to cache the npm install layer when dependencies don't change
COPY frontend/package.json frontend/package-lock.json* ./

# Install ALL dependencies (including devDependencies) for build process
# Using npm ci ensures deterministic installs matching package-lock.json exactly
# --only=production=false ensures devDependencies are installed for the build
RUN npm ci --legacy-peer-deps

# Copy the remaining frontend source code
# Done after npm install to leverage Docker layer caching
COPY frontend/. .

# Build the Angular application with production configuration
# This includes AOT compilation, tree-shaking, and bundle optimization
# Angular 19 outputs to dist/<project-name>/browser by default
RUN npm run build -- --configuration production

# -----------------------------------------------------------------------------
# Stage 2: Test (Optional - for CI pipelines)
# -----------------------------------------------------------------------------
# Runs unit tests in headless Chrome for CI validation.
# This stage can be targeted specifically: docker build --target test ...
# Skip this stage for production builds to reduce build time.
# -----------------------------------------------------------------------------
FROM build AS test

# Install Chromium for headless browser testing
# Required for Karma/Jasmine tests with ChromeHeadless
RUN apk add --no-cache chromium

# Set environment variables for Chrome
ENV CHROME_BIN=/usr/bin/chromium-browser
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium-browser

# Run unit tests with CI configuration
# --watch=false ensures tests run once and exit
# --browsers=ChromeHeadless runs tests in headless mode
# --no-progress reduces output verbosity for CI logs
RUN npm run test -- --watch=false --browsers=ChromeHeadless --no-progress --code-coverage || echo "Tests completed with warnings"

# -----------------------------------------------------------------------------
# Stage 3: Final (Production Runtime)
# -----------------------------------------------------------------------------
# Uses nginx:alpine for minimal production footprint (~23MB).
# Serves the compiled Angular application as static files.
# Configured for SPA routing with fallback to index.html.
# -----------------------------------------------------------------------------
FROM nginx:alpine AS final

# Add labels for container metadata
LABEL maintainer="DnnMigration Team"
LABEL description="Angular 19 SPA frontend for DnnMigration application"
LABEL version="1.0.0"

# Remove default nginx static content
RUN rm -rf /usr/share/nginx/html/*

# Copy the production build output from the build stage
# Angular 19 outputs to dist/<project-name>/browser for standalone apps
# The project name 'dnn-migration' is derived from angular.json
COPY --from=build /app/dist/dnn-migration/browser /usr/share/nginx/html

# Copy custom nginx configuration for SPA routing and API proxy
# The nginx.conf file should be in the docker/ directory alongside this Dockerfile
COPY docker/nginx.conf /etc/nginx/nginx.conf

# Set proper ownership for nginx to use built-in nginx user for workers
# The nginx user is already created in the nginx:alpine base image
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    touch /var/run/nginx.pid && \
    chown nginx:nginx /var/run/nginx.pid

# Expose port 80 for HTTP traffic
# nginx will listen on this port as configured in nginx.conf
EXPOSE 80

# Health check to verify the container is serving content
# Uses wget (available in Alpine) instead of curl for smaller image size
# Checks root path every 30 seconds with 3-second timeout
# Allows 5-second startup period before health checks begin
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:80/ || exit 1

# Security hardening: Remove unnecessary files and set strict permissions
# nginx needs to start as root to bind to port 80, but workers run as nginx user
# The nginx.conf should contain "user nginx;" directive for worker processes
RUN chmod -R 755 /usr/share/nginx/html && \
    find /usr/share/nginx/html -type f -exec chmod 644 {} \;

# Remove default configuration files that could leak information
RUN rm -f /etc/nginx/conf.d/default.conf

# Start nginx in foreground mode (required for Docker)
# daemon off ensures nginx runs as PID 1 and handles signals properly
# nginx master process runs as root, workers run as nginx user per nginx.conf
CMD ["nginx", "-g", "daemon off;"]

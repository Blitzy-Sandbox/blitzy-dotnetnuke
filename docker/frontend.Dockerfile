# Build stage
FROM node:20-alpine AS build
WORKDIR /app

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ .
RUN npm run build -- --configuration production

# Runtime stage
FROM nginx:alpine AS runtime

COPY --from=build /app/dist/dnn-migration/browser /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/nginx.conf

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:80 || exit 1

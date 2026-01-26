# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files and install dependencies
COPY package*.json ./
RUN npm ci

# Copy source and build
COPY . .
RUN npm run build -- --configuration production

# Runtime stage
FROM nginx:alpine AS runtime

# Copy nginx configuration
COPY --from=build /app/dist/dnn-migration-frontend/browser /usr/share/nginx/html
COPY ../docker/nginx.conf /etc/nginx/nginx.conf

EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]

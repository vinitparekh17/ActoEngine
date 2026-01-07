# Nginx Configuration for React SPA

Here is a recommended Nginx configuration for hosting the React application.

## 1. nginx.conf

Create a file named `nginx.conf` in your project root or deployment folder:

```nginx
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    # Gzip Compression
    gzip on;
    gzip_vary on;
    gzip_min_length 10240;
    gzip_proxied expired no-cache no-store private auth;
    gzip_types text/plain text/css text/xml text/javascript application/x-javascript application/xml;
    gzip_disable "MSIE [1-6]\.";

    # Security Headers
    add_header X-Frame-Options "SAMEORIGIN";
    add_header X-XSS-Protection "1; mode=block";
    add_header X-Content-Type-Options "nosniff";

    # Cache handling for static assets
    location /static/ {
        expires 1y;
        add_header Cache-Control "public, no-transform";
    }

    # SPA Routing - Forward all non-file requests to index.html
    location / {
        try_files $uri $uri/ /index.html;
        
        # Prevent caching of index.html to ensure updates are seen immediately
        add_header Cache-Control "no-store, no-cache, must-revalidate";
    }

    # Error pages
    error_page 500 502 503 504 /50x.html;
    location = /50x.html {
        root /usr/share/nginx/html;
    }
}
```

## 2. Dockerfile Integration

If using Docker, update your `Dockerfile`:

```dockerfile
# Build Stage
FROM node:18-alpine as build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Production Stage
FROM nginx:alpine
# Copy built assets
COPY --from=build /app/dist /usr/share/nginx/html
# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

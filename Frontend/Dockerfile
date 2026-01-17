# Build Stage
FROM node:20-alpine AS build
WORKDIR /app

ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Production Stage
FROM nginx:1.25-alpine
# Copy built assets
COPY --from=build /app/dist /usr/share/nginx/html
# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf
# Default API_BASE_URL (can be overridden at runtime)
ENV API_BASE_URL=http://localhost:5093
EXPOSE 80
# Run envsubst to substitute API_BASE_URL in nginx.conf at startup
CMD ["/bin/sh", "-c", "envsubst '${API_BASE_URL}' < /etc/nginx/conf.d/default.conf > /tmp/default.conf && mv /tmp/default.conf /etc/nginx/conf.d/default.conf && nginx -g 'daemon off;'"]

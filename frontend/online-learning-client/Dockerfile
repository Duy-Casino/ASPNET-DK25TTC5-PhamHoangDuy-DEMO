# syntax=docker/dockerfile:1
# Build context: frontend/online-learning-client   →   docker build -t eduviet-web ./frontend/online-learning-client

FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# API cùng tên miền (Nginx chuyển /api và /uploads sang API) nên để trống VITE_API_BASE_URL
ENV VITE_API_BASE_URL=
# Tên miền công khai (không có / ở cuối) cho thẻ chia sẻ Open Graph và canonical
ARG VITE_SITE_URL=http://localhost
ENV VITE_SITE_URL=${VITE_SITE_URL}
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s CMD wget -qO- http://localhost/ >/dev/null || exit 1

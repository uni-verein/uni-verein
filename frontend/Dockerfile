FROM node:20-alpine AS build

WORKDIR /app

ARG VITE_APP_VERSION
ARG VITE_APP_DEMO
ENV VITE_APP_VERSION=$VITE_APP_VERSION
ENV VITE_APP_DEMO=$VITE_APP_DEMO

COPY package*.json ./
RUN npm install || (echo "npm install failed, retrying..." && sleep 5 && npm install)

COPY . .
RUN npm run build

FROM nginx:alpine

RUN apk upgrade --no-cache --available

COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
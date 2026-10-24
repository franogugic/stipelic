# syntax=docker/dockerfile:1
#
# The web app: the Vite build (static dist/) served by Caddy on :8080, with the SPA fallback and long caching for the
# hashed assets. TLS, the API routes and the security headers are the edge proxy's job (deploy/Caddyfile).
#
# The build context is the FRONTEND checkout; this file and its ignore file live here, next to the rest of the deploy:
#   docker build -f deploy/frontend/Dockerfile -t luma-web:<tag> \
#     --build-arg VITE_PUBLIC_SITE_URL=https://example.com --build-arg VITE_SUPPORT_EMAIL=support@example.com \
#     ../frontend
# Dockerfile.dockerignore (next to this file) keeps node_modules, dist and every local .env file out of the context.

ARG NODE_VERSION=24
ARG CADDY_VERSION=2.10

FROM --platform=$BUILDPLATFORM node:${NODE_VERSION}-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN --mount=type=cache,id=luma-npm,target=/root/.npm npm ci --no-audit --no-fund

COPY . .

# Public values baked into the bundle (import.meta.env). Never pass a secret here: it ends up in the browser.
# VITE_API_BASE_URL stays empty: the API is served from the same origin (/api through the edge proxy).
ARG VITE_API_BASE_URL=""
ARG VITE_PUBLIC_SITE_URL=""
ARG VITE_SUPPORT_EMAIL=""
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL} \
    VITE_PUBLIC_SITE_URL=${VITE_PUBLIC_SITE_URL} \
    VITE_SUPPORT_EMAIL=${VITE_SUPPORT_EMAIL}

RUN npm run build

FROM caddy:${CADDY_VERSION}-alpine AS final
ARG GIT_SHA=unknown
LABEL org.opencontainers.image.title="luma-web" \
      org.opencontainers.image.description="Luma web app (static build)" \
      org.opencontainers.image.revision="${GIT_SHA}"

COPY --from=build /app/dist /srv
COPY <<"CADDYFILE" /etc/caddy/Caddyfile
# The web app container: static files only, plain HTTP on :8080 inside the Docker network. The edge proxy terminates
# TLS and adds the security headers.
{
	admin off
	auto_https off
	persist_config off
}

:8080 {
	root * /srv
	encode zstd gzip

	# Vite's hashed bundles never change under the same name: cache them for a year. A missing one is a 404, never
	# the app shell (a stale tab must not get HTML for a script).
	handle /assets/* {
		header Cache-Control "public, max-age=31536000, immutable"
		file_server
	}

	# Everything else (index.html, the email logo, favicons) is revalidated on every use, so a deploy shows at once.
	# Unknown paths are client-side routes: they get the app shell.
	handle {
		header Cache-Control "no-cache"
		try_files {path} /index.html
		file_server
	}
}
CADDYFILE

# Serves files only: no certificates, no admin API, nothing written at runtime, so it runs as "nobody".
USER 65534:65534
EXPOSE 8080

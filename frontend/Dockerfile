# DataBro frontend — build context is ./frontend (the whole pnpm workspace, because
# apps/* depend on packages/* via `workspace:*`).
#
# Build arg APP selects which app this image runs: "site" or "app".
#
# Targets:
#   dev      — `nuxt dev` with source bind-mounted by compose. HMR on every file change.
#   runtime  — Nuxt Nitro node-server output for staging/production.

ARG APP=site

# ---------------------------------------------------------------------------
# base: pnpm + workspace dependencies.
# ---------------------------------------------------------------------------
FROM node:24-alpine AS base
WORKDIR /workspace
ENV PNPM_HOME=/pnpm PATH=/pnpm:$PATH
RUN corepack enable

# Manifests first so the install layer caches across source edits.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml .npmrc tsconfig.base.json ./
COPY apps/site/package.json apps/site/
COPY apps/app/package.json apps/app/
COPY packages/ui/package.json packages/ui/
COPY packages/api-client/package.json packages/api-client/
COPY packages/types/package.json packages/types/
# `nuxt prepare` (postinstall) needs the app dirs to exist; skip scripts here and run
# prepare after the sources land.
RUN pnpm install --frozen-lockfile --ignore-scripts

# ---------------------------------------------------------------------------
# dev: hot-reload target used by the `apps` compose profile.
# node_modules live in the image (anonymous volumes in compose keep the Windows
# bind mount from shadowing them).
# ---------------------------------------------------------------------------
FROM base AS dev
ARG APP
ENV APP=${APP} \
    NODE_ENV=development \
    NUXT_HOST=0.0.0.0 \
    NUXT_PORT=3000 \
    CHOKIDAR_USEPOLLING=true \
    # No TTY here. When the workspace's dependencies drift — a package added on the host, or a
    # changed pnpm store path — pnpm wants to confirm before purging node_modules, finds no
    # terminal to ask, and aborts, which takes the container down on start. `CI=true` is pnpm's own
    # documented answer for non-interactive environments: proceed instead of asking.
    CI=true

COPY . .
RUN pnpm --filter "@databro/${APP}" exec nuxt prepare

EXPOSE 3000
CMD ["sh", "-c", "pnpm --filter @databro/${APP} dev --host 0.0.0.0 --port 3000"]

# ---------------------------------------------------------------------------
# build → runtime: the deployable image.
# ---------------------------------------------------------------------------
FROM base AS build
ARG APP
COPY . .
RUN pnpm --filter "@databro/${APP}" exec nuxt prepare \
 && pnpm --filter "@databro/${APP}" build

FROM node:24-alpine AS runtime
ARG APP
WORKDIR /app
ENV NODE_ENV=production \
    NITRO_HOST=0.0.0.0 \
    NITRO_PORT=3000
USER node
EXPOSE 3000
COPY --from=build --chown=node:node /workspace/apps/${APP}/.output ./
CMD ["node", "server/index.mjs"]

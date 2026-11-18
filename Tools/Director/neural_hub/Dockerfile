FROM node:22.23.4-bookworm-slim
WORKDIR /app/Tools/Director/neural_hub
COPY Tools/Director/neural_hub/package.json ./
RUN npm install --ignore-scripts --omit=dev --no-audit --no-fund && npm cache clean --force
COPY Tools/Director/neural_hub/ ./
WORKDIR /app
COPY docs/ARCHITECTURE.md ./docs/ARCHITECTURE.md
COPY Tools/Director/neural_catalog.json ./Tools/Director/neural_catalog.json
ENV NEURAL_HUB_LOCAL_ONLY=1 NEURAL_HUB_PORT=8765 NODE_ENV=production
USER node
WORKDIR /app/Tools/Director/neural_hub
CMD ["node","start.mjs"]

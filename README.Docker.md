# GoBike on Docker Desktop

## Start

Create `.env` from `.env.example`, then set the SQL Server password and map API keys.

```powershell
docker compose up -d --build
```

Open `http://localhost:5208`. The API is available at `http://localhost:5210` and SQL Server is exposed at `localhost,14330`.

Ollama runs on the Windows host. Keep Ollama running with model `qwen3:0.6b`; the API container connects to it through `host.docker.internal:11434`.

## Manage

```powershell
docker compose ps
docker compose logs -f api webui
docker compose stop
docker compose start
docker compose down
```

Database, uploads, and data-protection keys are stored in named Docker volumes. `docker compose down` keeps them; `docker compose down -v` deletes them.

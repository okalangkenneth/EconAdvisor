# EconAdvisor — Project Rules

## What This Project Is

AI-powered macroeconomic analysis assistant. A .NET 8 Minimal API fetches live
economic indicators from Riksbank, SCB, and World Bank APIs, stores them in
PostgreSQL, and calls a Dify RAG workflow that answers natural-language questions
like "Is Sweden heading into recession?" using real data + Riksbank policy PDFs.

**Portfolio differentiator**: .NET backend feeding live data *into* a Dify workflow —
a pattern none of the existing portfolio projects demonstrate.

---

## Memory Architecture (3-Layer System)

| Layer | Location | Purpose | Auto-Loaded |
|-------|----------|---------|-------------|
| **CLAUDE.md** | Project root | Rules, workflow, conventions | ✅ Always |
| **MEMORY.md** | `~/.claude/projects/<project>/memory/` | Session learnings | ✅ First 200 lines |
| **claude-mem** | `~/.claude-mem/` | Deep searchable history | ✅ Via MCP injection |

### Memory Commands

| Command | Purpose |
|---------|---------|
| `/memory` | View/toggle auto-memory, edit CLAUDE.md |
| `/remember` | Suggest patterns to save permanently |
| `/compact` | Instant (uses pre-written Session Memory) |
| `/dream` | Manually trigger memory consolidation |

---

## Anti-Hallucination Protocol

```
API / library question?       → Query docs FIRST, then answer
File content question?        → read_multiple_files FIRST
Uncertain about anything?     → Say "I need to verify" and use tools

NEVER:
  - Invent .NET method signatures or EF Core API
  - Guess Dify workflow JSON schema
  - Assume Riksbank/SCB API response shapes without reading the docs
  - Fabricate indicator values or economic data
```

### Confidence Levels

| Level | Meaning | Required Action |
|-------|---------|----------------|
| **HIGH** | Verified against docs/source | Can proceed |
| **MEDIUM** | Based on training, not verified | Flag it; verify before use |
| **LOW** | Educated guess | Must verify before any use |
| **UNKNOWN** | Cannot determine | State explicitly, do not guess |


---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  GitHub Pages Demo  (docs/index.html)                           │
│  Chart.js sparklines · Chat-style Q&A UI                        │
│  python -m http.server 8000 for local full-stack testing        │
└────────────────────┬────────────────────────────────────────────┘
                     │  fetch /api/analyse
┌────────────────────▼────────────────────────────────────────────┐
│  EconAdvisor.Api   (.NET 8 Minimal API)                         │
│  C:\Projects\EconAdvisor\EconAdvisor.Api\                       │
│                                                                  │
│  GET  /api/indicators/{country}/{series}  → PostgreSQL cache    │
│  POST /api/analyse                        → fetch + Dify call   │
│                                                                  │
│  Data sources:                                                   │
│    Riksbank REST API  (policy rate, yield curve)                 │
│    SCB PxWeb API      (CPIF, unemployment)                       │
│    World Bank API v2  (GDP growth)                               │
│                                                                  │
│  EF Core + Npgsql → PostgreSQL 16                               │
│  Serilog → structured console + ELK (Phase 5)                   │
│  FluentValidation for request validation                         │
└────────────────────┬────────────────────────────────────────────┘
                     │  POST http://localhost/v1/workflows/run
                     │  Authorization: Bearer DIFY_WORKFLOW_KEY
┌────────────────────▼────────────────────────────────────────────┐
│  Dify Workflow  (localhost — existing Docker stack)              │
│  C:\Projects\dify-etsy-toolkit\dify\docker\                     │
│                                                                  │
│  Input:  structured indicator JSON + user question               │
│  RAG:    Riksbank monetary policy PDFs (uploaded to Dify)        │
│  LLM:    Claude (via ANTHROPIC_API_KEY in Dify .env)            │
│  Output: analysis text + source citations (JSON)                 │
└─────────────────────────────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────────┐
│  PostgreSQL 16  (Docker)                                         │
│  Schema: indicator_series, indicator_observations                │
│  Cache TTL: 1 hour for live indicators                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Key Economic Indicators

| Indicator | Source API | Why it matters |
|-----------|-----------|----------------|
| Policy rate (reporänta) | Riksbank | Monetary policy signal |
| CPIF inflation | SCB PxWeb | Riksbank's target measure |
| Unemployment rate | SCB PxWeb | Labour market health |
| GDP growth (Sweden) | World Bank | Output trend |
| Real interest rate | Derived (rate − CPIF) | True cost of borrowing |
| Yield curve slope | Riksbank | Recession leading indicator |


---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8 Minimal API |
| ORM | EF Core 8 + Npgsql |
| Database | PostgreSQL 16 (Docker) |
| Validation | FluentValidation |
| HTTP client | `IHttpClientFactory` (typed clients per data source) |
| Logging | Serilog → Console + Elasticsearch (Phase 5) |
| AI/RAG | Dify 1.13.2 workflow at `http://localhost` |
| Demo UI | GitHub Pages — Chart.js + vanilla JS chat UI |
| Container | Docker Compose |
| Observability | ELK stack Elasticsearch 7.17 + Kibana 7.17 (Phase 5) |

---

## Dify Setup (Existing — Do Not Recreate)

- **Dify console**: `http://localhost` (not `/app`, not `:3000`)
- **Dify API base**: `http://localhost/v1`
- **Workflow API key**: stored in `.env` as `DIFY_WORKFLOW_KEY`
- **docker-compose.override.yml**: `C:\Projects\dify-etsy-toolkit\dify\docker\`
- **Recovery script**: `C:\Projects\dify-recover.ps1`
- **ANTHROPIC_API_KEY**: already in Dify `.env` — do not duplicate

---

## Project Layout

```
C:\Projects\EconAdvisor\
├── CLAUDE.md
├── .env                           ← secrets (gitignored)
├── .env.example                   ← committed template
├── .gitignore
├── docker-compose.yml             ← PostgreSQL + (Phase 5) ELK
├── EconAdvisor.Api\
│   ├── EconAdvisor.Api.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Data\
│   │   ├── EconContext.cs
│   │   └── Migrations\
│   ├── Models\
│   │   ├── IndicatorSeries.cs
│   │   └── IndicatorObservation.cs
│   ├── Services\
│   │   ├── RiksbankClient.cs
│   │   ├── ScbClient.cs
│   │   ├── WorldBankClient.cs
│   │   └── DifyWorkflowClient.cs
│   ├── Endpoints\
│   │   ├── IndicatorEndpoints.cs
│   │   └── AnalyseEndpoints.cs
│   └── Validators\
│       └── AnalyseRequestValidator.cs
├── docs\                          ← GitHub Pages demo
│   ├── index.html
│   ├── app.js
│   └── style.css
└── dify-workflow\
    ├── workflow-export.json
    └── policy-docs\
        └── README.md              ← links to Riksbank PDF sources
```


---

## Docker Commands

```bash
# Always build fresh — never use restart (runs stale binaries)
docker-compose up -d --build

# Rebuild only API without touching DB
docker-compose up -d --build econadvisor-api

# View logs
docker-compose logs -f econadvisor-api
```

---

## Git Conventions

- **Repo**: `https://github.com/okalangkenneth/EconAdvisor`
- **Branch**: `master` (match existing portfolio repos)
- **Commits**: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`
- **Push**: after every completed phase — `git push origin master`
- **Never commit**: `.env`, `policy-docs/` PDFs, `bin/`, `obj/`

### First-Time Setup
```bash
git init
git add .
git commit -m "feat: initial EconAdvisor scaffold"
git remote add origin https://github.com/okalangkenneth/EconAdvisor.git
git branch -M master
git push -u origin master
```

---

## Critical Rules

### API Data Freshness
- Cache indicator observations in PostgreSQL with a `fetched_at` timestamp
- Re-fetch from source if `fetched_at < NOW() - INTERVAL '1 hour'`
- Never call external APIs on every request — respect rate limits

### Environment Variables (NO HARDCODING)
```
POSTGRES_CONNECTION=Host=localhost;Port=5432;Database=econadvisor;...
DIFY_API_BASE=http://localhost/v1
DIFY_WORKFLOW_KEY=app-xxxxxxxxxxxx
RIKSBANK_API_BASE=https://api.riksbank.se/swea/v1
SCB_API_BASE=https://api.scb.se/OV0104/v1/doris/sv/ssd
WORLDBANK_API_BASE=https://api.worldbank.org/v2
```

### Code Quality
- No placeholders (`YOUR_API_KEY`, `TODO`, `FIXME`) in committed code
- Remove unused `using` statements
- Log every external API call: source, series, duration, status code
- Return `ProblemDetails` (RFC 7807) for all API errors

### Before Every Change
- Read the actual file before editing — use `read_multiple_files`
- Only modify what was explicitly requested
- Ask if <90% confident about an API or EF Core behaviour


---

## Build Progress (Keep Updated)

**Claude Code: update this section at the end of every session.**

### ✅ COMPLETED
- CLAUDE.md, .env.example, .gitignore created

### 🔨 IN PROGRESS
- Phase 1: Project scaffold + PostgreSQL schema

### ❌ REMAINING
- Phase 1: .NET solution, EF Core models, Docker DB, git init + first push
- Phase 2: Riksbank / SCB / World Bank typed HTTP clients + PostgreSQL cache
- Phase 3: Dify workflow build (upload PDFs, RAG nodes, LLM node, test API)
- Phase 4: POST /api/analyse — full data + Dify integration
- Phase 5: Serilog + ELK (Elasticsearch 7.17 + Kibana 7.17)
- Phase 6: GitHub Pages Chart.js demo + README + LinkedIn post

---

## Phase Plan

### Phase 1 — Scaffold & Database Schema
**Goal**: Running PostgreSQL, compilable .NET solution, git repo created.

Deliverables:
- `docker-compose.yml` with `postgres:16` service
- `EconAdvisor.Api.csproj` with packages: `Npgsql.EntityFrameworkCore.PostgreSQL`,
  `FluentValidation.AspNetCore`, `Serilog.AspNetCore`, `Serilog.Sinks.Console`,
  `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore`
- `EconContext.cs` with `IndicatorSeries` and `IndicatorObservation` entities
- `Program.cs`: minimal API skeleton, Swagger, health check
- EF Core migration applied — tables exist in DB
- `.env` + `.env.example` + `.gitignore`
- GitHub repo created and initial commit pushed

Verification:
```bash
docker-compose up -d
dotnet build EconAdvisor.Api
dotnet ef database update
curl http://localhost:5000/health   # → {"status":"Healthy"}
```

### Phase 2 — Data Pipeline (External API Clients + Cache)
**Goal**: All indicators fetching live data and caching in PostgreSQL.

Deliverables:
- `RiksbankClient.cs` — policy rate + yield curve series
- `ScbClient.cs` — SCB PxWeb JSON-stat: CPIF + unemployment
- `WorldBankClient.cs` — World Bank v2: Sweden GDP growth
- `IndicatorService.cs` — fetch-or-cache logic (TTL 1 hour)
- `GET /api/indicators/{country}/{series}` endpoint
- Derived: real interest rate computed on the fly

Verification:
```bash
curl http://localhost:5000/api/indicators/SE/policy_rate
curl http://localhost:5000/api/indicators/SE/cpif
# Second call is cache hit (check logs: "cache hit")
```


### Phase 3 — Dify Workflow Build
**Goal**: Working Dify workflow; returns analysis from .NET HTTP call.

Deliverables:
- Riksbank Monetary Policy Report PDFs uploaded to Dify Knowledge Base
- Dify workflow nodes:
  1. **Input**: `question` (string) + `indicators` (JSON object)
  2. **Knowledge Retrieval**: RAG over uploaded PDFs
  3. **LLM**: Claude — economist persona, indicator data + retrieved chunks as context
  4. **Output**: `{ analysis: string, citations: string[] }`
- `DIFY_WORKFLOW_KEY` saved to `.env`
- `DifyWorkflowClient.cs` — POSTs to `http://localhost/v1/workflows/run`

Verification:
```bash
curl -X POST http://localhost/v1/workflows/run \
  -H "Authorization: Bearer $DIFY_WORKFLOW_KEY" \
  -H "Content-Type: application/json" \
  -d '{"inputs":{"question":"Is Sweden heading into recession?",
       "indicators":{"policy_rate":3.5,"cpif":2.3}},
       "response_mode":"blocking","user":"test"}'
```

### Phase 4 — Analyse Endpoint (Full Integration)
**Goal**: POST /api/analyse returns real AI analysis grounded in live data.

Deliverables:
- `AnalyseRequest`: `{ question: string, country: string = "SE" }`
- `AnalyseResponse`: `{ question, indicators, analysis, citations, generatedAt }`
- `POST /api/analyse`: validate → fetch indicators → call Dify → return response
- Error handling: Dify unavailable → `503 ProblemDetails`

Verification:
```bash
curl -X POST http://localhost:5000/api/analyse \
  -H "Content-Type: application/json" \
  -d '{"question":"Is Sweden heading into recession?","country":"SE"}'
```

### Phase 5 — Observability (Serilog + ELK)
**Goal**: Structured logs in Kibana. Same pattern as DotNetMicroservices_1.

Deliverables:
- `docker-compose.yml` extended: `elasticsearch:7.17` + `kibana:7.17`
- `Serilog.Sinks.Elasticsearch` configured
- Index pattern `econdadvisor-logs-*` in Kibana
- Key log events: external API call (source, series, duration_ms, cached),
  Dify call (question truncated, duration_ms, status)

Verification:
- `http://localhost:5601` — index pattern shows events
- Filter `fields.SourceContext: RiksbankClient` shows API call logs

### Phase 6 — GitHub Pages Demo + README + LinkedIn Post
**Goal**: Public demo + polished portfolio artifact.

Deliverables:
- `docs/index.html` — dark "financial terminal" aesthetic
  - Pre-loaded question buttons
  - 6 Chart.js sparklines (last 12 months per indicator)
  - AI answer panel + citation list
- `docs/app.js` — fetches `POST /api/analyse` + `GET /api/indicators/SE`
- `README.md` with Mermaid architecture diagram + indicator table + setup guide
- `LINKEDIN_POST.md` — includes tech stack + "what I'd add for production" section
- Final push + add to LinkedIn Featured section

Local testing: `python -m http.server 8000` from `docs/` folder

---

## Corrections Log

| Date | Mistake | Rule Added |
|------|---------|-----------|
| | | |

---

## Session Management

### Starting a Session
1. CLAUDE.md auto-loads — trust it, don't re-explore the codebase
2. Run `docker-compose up -d` if DB isn't running
3. Check Build Progress above for current state
4. Use `read_multiple_files` to read actual files before editing them

### Ending a Session
```
Update Build Progress in CLAUDE.md (mark completed, move remaining), then stop.
```

### After Every Phase
```bash
git add .
git commit -m "feat: Phase X — <short description>"
git push origin master
```

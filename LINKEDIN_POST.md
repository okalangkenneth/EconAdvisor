I built an AI macroeconomic analyst that answers "Is Sweden heading into recession?" using live data.

Here's the architecture:

→ A .NET 8 API fetches live indicators from Riksbank, SCB, and World Bank APIs
→ Stores them in PostgreSQL with a 1-hour cache (no hammering public APIs)
→ Passes the latest values as structured context into a Dify RAG workflow
→ Dify retrieves relevant passages from Riksbank monetary policy reports
→ Claude synthesises a grounded, cited analysis in natural language

The result is a macroeconomic briefing anchored to real numbers — policy rate, CPIF inflation, unemployment, GDP growth, real interest rate, and yield curve slope — with source citations from the actual documents.

---

**Why I built this**

I have a master's in Economics (Nationalekonomi) and wanted to combine that domain knowledge with my .NET backend work. The interesting engineering challenge: making a .NET API feed live data *into* an AI workflow, rather than using AI as a standalone tool.

None of my previous projects did this. Now one does.

---

**The data pipeline was the hard part**

Three different APIs, three different response formats:

• Riksbank SWEA v1 — clean REST, daily observations
• SCB PxWeb — returns JSON-stat (a statistical format with flat value arrays and stride-based indexing). Not JSON. Required a custom parser.
• World Bank v2 — wraps data in an outer array: [{metadata}, [{data}]]. Easy to miss.

And the variable codes in SCB's API don't match the Swedish display names. "Kön" in the UI is "Kon" in the API. "Arbetskraftstillhörighet" is "Arbetskraftstillh". Got a 400 error until I fetched the table metadata and used the actual code field.

---

**Running Dify on Windows Docker had its own surprises**

• The plugin daemon uses `uv` to create Python virtual environments. `uv` uses reflinks (copy-on-write) which fail on NTFS-mounted Docker volumes with os error 95. Fixed by mounting the working directory as a Docker named volume (Linux ext4) and setting UV_LINK_MODE=copy.

• `host.docker.internal` didn't resolve inside the worker container, so Ollama embedding calls timed out. Fixed with extra_hosts: host.docker.internal:host-gateway.

• The api and worker containers had different host paths for the same /app/api/storage volume mount (C: vs E: drive) because docker-compose had been run from different directories. Worker couldn't find files the api uploaded. Fixed by pinning both to the same absolute path in docker-compose.override.yml.

None of these appear in the documentation. All of them will now appear in my CLAUDE.md.

---

**Stack**
.NET 8 Minimal API · EF Core + Npgsql · PostgreSQL 16 · Dify 1.13.2 · Claude Haiku · Ollama (nomic-embed-text) · Serilog + ELK · Chart.js · Docker Compose · GitHub Pages

---

**What I'd add for production**
• Redis distributed cache — replace PostgreSQL TTL for horizontal scaling
• Rate limiting on /api/analyse — it calls a paid LLM; open endpoints are expensive
• Authentication — same reason
• More Riksbank PDFs in the knowledge base — currently 2 reports; more = better RAG recall
• Structured citation extraction — surface actual page numbers from retrieved chunks

🔗 github.com/okalangkenneth/EconAdvisor

#DotNet #AI #MachineLearning #Economics #Dify #RAG #Docker #BuildInPublic #Sweden

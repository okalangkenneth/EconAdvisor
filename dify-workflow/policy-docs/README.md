# Policy & Outlook Documents — Knowledge Base Sources

These PDFs are uploaded to Dify as a single Knowledge Base named "EconAdvisor Policy Docs".
They are NOT committed to git (too large for version control).

## What belongs here and why

The Knowledge Base provides the *qualitative* layer — policy reasoning, forecasts,
and economic commentary. The *quantitative* layer (actual numbers) is already
handled by the .NET API fetching live data from each source's REST API.

| Layer | What it covers | How it enters the workflow |
|-------|---------------|---------------------------|
| Live indicators | Policy rate, CPIF, unemployment, GDP, yield curve | .NET API → indicators JSON input |
| Policy documents | *Why* the numbers are what they are; forecasts; methodology | RAG knowledge base |

---

## Documents to include

### Riksbank (primary — monetary policy)
| Document | URL |
|----------|-----|
| Monetary Policy Report Feb 2025 | https://www.riksbank.se/en-gb/monetary-policy/monetary-policy-report/ |
| Monetary Policy Report Nov 2024 | https://www.riksbank.se/en-gb/monetary-policy/monetary-policy-report/ |

These explain the Riksbank's reasoning for rate decisions and inflation/growth forecasts.

### SCB — Statistics Sweden (labour market & inflation methodology)
| Document | URL |
|----------|-----|
| Economic Outlook / Konjunkturläget | https://www.scb.se/en/finding-statistics/statistics-by-subject-area/national-accounts/economic-surveys/economic-outlook/ |

SCB publishes quarterly economic outlooks with narrative context around CPIF and unemployment.

### World Bank (international perspective on Sweden)
| Document | URL |
|----------|-----|
| Sweden Country Economic Memorandum / Overview | https://www.worldbank.org/en/country/sweden |
| Global Economic Prospects (Sweden section) | https://www.worldbank.org/en/publication/global-economic-prospects |

World Bank adds context on how Sweden's GDP trajectory compares internationally.

---

## How to create the Knowledge Base in Dify

1. Go to `http://localhost` → **Knowledge** → **Create Knowledge**
2. Name it: `EconAdvisor Policy Docs`
3. Upload all PDFs above
4. Wait for indexing to complete (green status)
5. Open the imported workflow → click **Search Policy Docs** node
6. Click **+ Add** → select `EconAdvisor Policy Docs` → Save → **Publish**

# adessoGPT Mini

Ein Chatbot-Projekt mit MCP (Model Context Protocol) Integration über das Streamable HTTP Protokoll.

## Architektur

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────┐
│   Frontend  │────▶│  .NET Backend    │────▶│  MCP Server │
│   (React)   │     │  + Gemini LLM    │     │  (Python)   │
└─────────────┘     └──────────────────┘     └─────────────┘
                           │                        │
                           │   MCP Streamable HTTP  │
                           │◀──────────────────────▶│
                           │                        │
                    ┌──────┴──────┐          ┌──────┴──────┐
                    │ Function    │          │   Tools     │
                    │ Calling     │          │  - add      │
                    └─────────────┘          │  - get_time │
                                             └─────────────┘
```

### Komponenten

| Komponente | Technologie | Beschreibung |
|------------|-------------|--------------|
| **Frontend** | React + Vite | Chat-Interface für Benutzerinteraktion |
| **Backend** | .NET 10 + Gemini | API-Server mit LLM Function Calling |
| **MCP Client** | Custom HTTP Client | Implementiert MCP Streamable HTTP Protokoll |
| **MCP Server** | Python + FastMCP | Stellt Tools über MCP bereit |

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Python 3.11+](https://www.python.org/)
- [Google AI API Key](https://aistudio.google.com/apikey)

## Installation

### 1. Repository klonen

```bash
git clone <repository-url>
cd adesso-gpt
```

### 2. Umgebungsvariablen konfigurieren

```bash
cp .env.example .env
```

Bearbeite `.env` und setze deinen API Key:

```
GEMINI_API_KEY=dein_api_key_hier
MCP_URL=http://localhost:8000/mcp
```

### 3. MCP Server (Python)

```bash
cd mcp-server
pip install mcp[cli] python-dotenv
```

### 4. Backend (.NET)

```bash
cd backend-dotnet/AdessoGpt.Api
dotnet restore
```

### 5. Frontend (React)

```bash
cd frontend
npm install
```

## Starten

**Wichtig:** Alle drei Komponenten müssen gleichzeitig laufen.

### Terminal 1: MCP Server

```bash
cd mcp-server
python server.py
# Läuft auf http://localhost:8000/mcp
```

### Terminal 2: Backend

```bash
cd backend-dotnet/AdessoGpt.Api
dotnet run
# Läuft auf http://localhost:5087
```

### Terminal 3: Frontend

```bash
cd frontend
npm run dev
# Läuft auf http://localhost:5173
```

Öffne http://localhost:5173 im Browser.

## Verwendung

Der Chatbot kann die MCP-Tools verwenden. Beispiel-Prompts:

| Prompt | Tool | Erwartete Antwort |
|--------|------|-------------------|
| "Was ist 42 + 17?" | `add` | "59" |
| "Wie spät ist es?" | `get_time` | Aktuelle Uhrzeit |
| "Addiere 100 und 200" | `add` | "300" |

## MCP Tools

Der MCP Server stellt folgende Tools bereit:

### `add(a: int, b: int) -> int`
Addiert zwei ganze Zahlen.

### `get_time(tz: str = "Europe/Berlin") -> str`
Gibt die aktuelle Zeit als ISO-String zurück.

## Technische Details

### MCP Streamable HTTP Protokoll

Der `McpHttpClient` implementiert das MCP Streamable HTTP Protokoll:

1. **Initialize Handshake**: `initialize` + `notifications/initialized`
2. **Session Management**: `Mcp-Session-Id` Header
3. **Tool Calling**: `tools/call` JSON-RPC Requests
4. **SSE Parsing**: Extrahiert JSON aus Server-Sent Events

### Function Calling Flow

```
1. User: "Was ist 5 + 3?"
2. Backend → Gemini: Prompt + Tool-Definitionen
3. Gemini: Tool Call Request (add, a=5, b=3)
4. Backend → MCP Server: tools/call (add)
5. MCP Server: Führt add(5, 3) aus → 8
6. Backend ← MCP Server: Result (8)
7. Gemini: Formuliert Antwort
8. User: "Das Ergebnis ist 8."
```

## Projektstruktur

```
adesso-gpt/
├── .env.example          # Umgebungsvariablen Template
├── backend-dotnet/
│   └── AdessoGpt.Api/
│       ├── Program.cs        # API Endpoints + Gemini Integration
│       └── McpHttpClient.cs  # MCP Streamable HTTP Client
├── frontend/
│   └── src/
│       └── App.tsx           # Chat UI
└── mcp-server/
    └── server.py             # MCP Server mit Tools
```

## Lizenz

MIT

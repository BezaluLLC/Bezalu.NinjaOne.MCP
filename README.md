# Bezalu NinjaOne MCP Server

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that provides AI assistants with secure, user-delegated access to the [NinjaOne](https://www.ninjaone.com) RMM platform.

## Features

- **Embedded OAuth 2.0 Authorization Server with Dynamic Client Registration (DCR)** — Spec-compliant with MCP 2025-11-25; works with any conformant MCP client even though NinjaOne and Entra ID do not support DCR
- **Per-user delegated access** — Actions are performed as the authenticated user; the server never uses a privileged app token for NinjaOne calls
- **Cloud-neutral local persistence** — Clients and tokens are stored on local disk under a configurable, volume-mountable path (no cloud dependency)
- **High-value NinjaOne tools** — Devices, Organizations, Locations, Alerts, Custom Fields, Software, Windows Services, Jobs, Tasks, Security & Patching
- **Container-first** — Distributed as a Linux container image via GitHub Container Registry
- **Minimal footprint** — Self-contained single-file publish, ideal for Azure Container Apps

## Container Image

```bash
docker pull ghcr.io/bezalullc/ninjaone-mcp:latest
```

## Configuration

The server requires the following environment variables:

| Variable | Required | Description |
|----------|----------|-------------|
| `NINJAONE_INSTANCE` | Yes | NinjaOne instance URL (e.g., `https://app.ninjarmm.com`, `https://eu.ninjarmm.com`) |
| `NINJAONE_CLIENT_ID` | Yes | Client ID of the single NinjaOne API application used to bridge user authentication |
| `NINJAONE_CLIENT_SECRET` | Yes | Client secret of that NinjaOne API application |
| `NINJAONE_SCOPES` | No | OAuth scopes requested upstream and advertised in metadata (default: `monitoring management offline_access`) |
| `MCP_DATA_PATH` | No | Directory for local persistence of registered clients and tokens (default: `/app/data`) |
| `PUBLIC_BASE_URL` | No | Explicit externally-visible base URL (e.g., `https://mcp.example.com`). When unset, it is derived from the request, honoring `X-Forwarded-*` headers |

### Local persistence & volume mount

The server is its own OAuth authorization server, so it persists registered clients and issued
tokens locally — no cloud or external database is required. Storage is a small set of JSON files
under `MCP_DATA_PATH` (default `/app/data`):

| File | Contents |
|------|----------|
| `clients.json` | Dynamically-registered MCP clients (RFC 7591) |
| `tokens.json` | Issued access/refresh tokens mapped to their upstream NinjaOne token set |

In-flight authorization sessions and single-use authorization codes are short-lived and kept in
memory only. Mount a volume at `MCP_DATA_PATH` to retain registrations and user sessions across
container restarts.

> **Permissions:** the container runs as a non-root user. The image pre-creates `/app/data` with
> the correct ownership, and Docker **named volumes** (as in the example below) inherit it
> automatically. If you instead use a **host bind mount** (e.g. `-v /srv/mcp:/app/data`), make the
> host directory writable by the runtime user first, for example `chown -R 1654:1654 /srv/mcp`.

## Running

```bash
docker run -d \
  -p 8080:8080 \
  -e NINJAONE_INSTANCE=https://app.ninjarmm.com \
  -e NINJAONE_CLIENT_ID=your-ninjaone-client-id \
  -e NINJAONE_CLIENT_SECRET=your-ninjaone-client-secret \
  -v ninjaone-mcp-data:/app/data \
  ghcr.io/bezalullc/ninjaone-mcp:latest
```

## MCP Client Configuration

Connect your MCP client to the running server:

```json
{
  "servers": {
    "ninjaone": {
      "type": "http",
      "url": "http://localhost:8080"
    }
  }
}
```

The MCP client discovers the server's OAuth authorization server metadata, dynamically registers
itself (DCR), and runs the Authorization Code + PKCE flow automatically — the user authenticates
with NinjaOne in their browser, and the resulting per-user token is used for all subsequent API
calls.

## Available Tools

### Devices
| Tool | Description |
|------|-------------|
| `list_devices` | List all devices with pagination |
| `list_devices_detailed` | List devices with full hardware/OS details |
| `get_device` | Get a specific device by ID |
| `search_devices` | Search devices by name |

### Organizations & Locations
| Tool | Description |
|------|-------------|
| `list_organizations` | List all organizations |
| `list_organizations_detailed` | List organizations with full details |
| `get_organization` | Get a specific organization by ID |
| `list_locations` | List locations (all or per-org) |

### Alerts
| Tool | Description |
|------|-------------|
| `list_alerts` | List active alerts |
| `reset_alert` | Acknowledge/reset an alert |

### Custom Fields
| Tool | Description |
|------|-------------|
| `list_custom_fields` | List custom field definitions |
| `get_device_custom_fields` | Get custom field values for a device |
| `update_device_custom_fields` | Update custom field values for a device |

### Software & Patching
| Tool | Description |
|------|-------------|
| `query_installed_software` | Query installed software across all devices |
| `list_software_products` | List known software products |
| `query_software_patches` | Query pending software patches |
| `query_software_patch_installs` | Query software patch installation history |
| `query_os_patches` | Query pending OS patches |
| `query_os_patch_installs` | Query OS patch installation history |

### Windows Services
| Tool | Description |
|------|-------------|
| `query_windows_services` | Query Windows services across fleet (filter by name/state) |

### Jobs & Tasks
| Tool | Description |
|------|-------------|
| `list_jobs` | List scheduled/running jobs (scripts, patching, maintenance) |
| `list_tasks` | List automation tasks |

### Security & Health
| Tool | Description |
|------|-------------|
| `query_antivirus_status` | Query AV protection status across devices |
| `query_antivirus_threats` | Query detected antivirus threats |
| `query_device_health` | Query overall device health status |

## NinjaOne OAuth Setup

1. In NinjaOne Administration → Apps → API, create an API application
2. Set the **Authorization Grant Type** to "Authorization Code"
3. Set the **Redirect URI** to this server's callback URL: `<PUBLIC_BASE_URL>/callback`
   (e.g., `https://mcp.example.com/callback` or `http://localhost:8080/callback` for local testing)
4. Note the **Client ID** and **Client Secret** and provide them via `NINJAONE_CLIENT_ID` / `NINJAONE_CLIENT_SECRET`
5. Set the appropriate scopes (monitoring, management, etc.)

> Only this single NinjaOne API application credential is needed. MCP clients never see it — they
> register dynamically against this server and receive tokens this server issues.

## OAuth Endpoints

The server exposes a spec-compliant OAuth 2.0 authorization server:

| Endpoint | Purpose |
|----------|---------|
| `GET /.well-known/oauth-authorization-server` | Authorization server metadata (RFC 8414) |
| `POST /register` | Dynamic Client Registration (RFC 7591) |
| `GET /authorize` | Authorization endpoint (PKCE required) |
| `GET /callback` | NinjaOne authorization-code callback |
| `POST /token` | Token endpoint (`authorization_code`, `refresh_token`) |

## Development

```bash
# Set environment variables
export NINJAONE_INSTANCE=https://app.ninjarmm.com
export NINJAONE_CLIENT_ID=your-ninjaone-client-id
export NINJAONE_CLIENT_SECRET=your-ninjaone-client-secret
export MCP_DATA_PATH=./data

# Run locally
dotnet run
```

## Architecture

```
MCP Client (e.g., VS Code, Claude Desktop)
    │
    ├─ 1. Discover metadata + Dynamic Client Registration ─► MCP Server (this container)
    │                                                            │  (OAuth Authorization Server)
    ├─ 2. /authorize (PKCE) ─────────────────────────────────►  │
    │                                                            ├─ Redirects user to NinjaOne
    │                                                            │
    │        NinjaOne OAuth Server ◄── user authenticates ──────┤
    │                  │                                         │
    │                  └─► /callback (auth code) ───────────────►├─ Exchanges code (single app cred)
    │                                                            │  Stores per-user NinjaOne tokens
    ├─ 3. /token (code + PKCE verifier) ─────────────────────►  │  Issues this server's token
    │◄── access_token + refresh_token ──────────────────────────┤
    │                                                            │
    ├─ 4. Bearer <token> ──────────────────────────────────────►├─ Validates token, resolves the
    │                                                            │  user's NinjaOne token (refreshes
    │                                                            │  transparently), calls NinjaOne
    │◄── Tool Results ──────────────────────────────────────────┘  as the user
```

## License

See [LICENSE](LICENSE) for details.


## More information

ASP.NET Core MCP servers use the [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) package from the MCP C# SDK. For more information about MCP:

- [Official Documentation](https://modelcontextprotocol.io/)
- [Protocol Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Organization](https://github.com/modelcontextprotocol)
- [MCP C# SDK](https://modelcontextprotocol.github.io/csharp-sdk)

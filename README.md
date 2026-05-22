# Bezalu NinjaOne MCP Server

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that provides AI assistants with secure, user-delegated access to the [NinjaOne](https://www.ninjaone.com) RMM platform.

## Features

- **OAuth Authorization Code Flow** — Actions are performed as the authenticated user via MCP-native OAuth
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
| `NINJAONE_SCOPES` | No | OAuth scopes advertised in resource metadata (default: `monitoring management offline_access`) |

## Running

```bash
docker run -d \
  -p 8080:8080 \
  -e NINJAONE_INSTANCE=https://app.ninjarmm.com \
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

The MCP client will handle the OAuth Authorization Code Flow automatically — the user will be prompted to authenticate with NinjaOne in their browser, and the resulting token will be used for all subsequent API calls.

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
3. Set the **Redirect URI** to match your MCP client's callback URL
4. Note the **Client ID** and **Client Secret**
5. Set the appropriate scopes (monitoring, management, etc.)

## Development

```bash
# Set environment variables
export NINJAONE_INSTANCE=https://app.ninjarmm.com

# Run locally
dotnet run
```

## Architecture

```
MCP Client (e.g., VS Code, Claude Desktop)
    │
    ├─ OAuth Authorization Code Flow ──► NinjaOne OAuth Server
    │                                         │
    │◄── Access Token ────────────────────────┘
    │
    ├─ Bearer Token ──► MCP Server (this container)
    │                       │
    │                       ├─ Extracts token from request
    │                       ├─ Creates per-request NinjaOne.Client
    │                       └─ Calls NinjaOne API as the user
    │
    │◄── Tool Results ──────┘
```

## License

See [LICENSE](LICENSE) for details.


## More information

ASP.NET Core MCP servers use the [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) package from the MCP C# SDK. For more information about MCP:

- [Official Documentation](https://modelcontextprotocol.io/)
- [Protocol Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Organization](https://github.com/modelcontextprotocol)
- [MCP C# SDK](https://modelcontextprotocol.github.io/csharp-sdk)

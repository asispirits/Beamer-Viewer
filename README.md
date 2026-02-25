# Notifications

Notifications is a Windows desktop app (WPF + WebView2) that displays Beamer posts in a local UI, tracks acknowledged views, and preserves message history in a local archive.

## Quick Start

### Build

```bash
cd <repo-root>
dotnet build Notifications.csproj -c Release -nologo
```

### Publish

```bash
cd <repo-root>
dotnet publish Notifications.csproj -c Release -r win-x64 --self-contained true -nologo
```

Published executable:

`bin/Release/net8.0-windows/win-x64/publish/Notifications.exe`

## Config File

Primary config file:

`Notifications.config.json`

At minimum, set:

- `ApiKey`

## Documentation Suite

Complete documentation is under:

`docs/README.md`

Direct links:

- User guide: `docs/USER_GUIDE.md`
- Config reference: `docs/CONFIG_REFERENCE.md`
- Technical architecture: `docs/TECHNICAL_ARCHITECTURE.md`
- API/message contracts: `docs/API_AND_MESSAGE_CONTRACTS.md`
- Operations runbook: `docs/OPERATIONS_RUNBOOK.md`
- Troubleshooting: `docs/TROUBLESHOOTING.md`
- Developer guide: `docs/DEVELOPER_GUIDE.md`
- Branding customization guide: `docs/BRANDING_CUSTOMIZATION_GUIDE.md`
- Screenshot checklist: `docs/SCREENSHOT_CHECKLIST.md`

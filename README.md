<h1 align="center" style="border-bottom: none">
    LuaDNS DNS Provider
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/luadns-dnsplugin/actions/workflows/keyfactor-starter-workflow.yml"><img src="https://github.com/Keyfactor/luadns-dnsplugin/actions/workflows/keyfactor-starter-workflow.yml/badge.svg" alt="Build" /></a>
<a href="https://github.com/Keyfactor/luadns-dnsplugin/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/luadns-dnsplugin?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/luadns-dnsplugin?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/luadns-dnsplugin/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#requirements">
    <b>Requirements</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=dnsplugin">
    <b>Related Integrations</b>
  </a>
</p>

## Overview

The LuaDNS Provider plugin enables automated DNS-based domain validation for Keyfactor certificate lifecycle management through LuaDNS. This plugin integrates with the LuaDNS API to automatically create, verify, and delete DNS TXT records required for domain validation during certificate issuance and renewal.

## Features

- Automated DNS TXT record creation and deletion in LuaDNS
- HTTP Basic authentication using the account's username (email) and API key
- Automatic zone discovery across all zones on the account, matched by longest domain suffix

## Requirements

### Keyfactor Platform
- Keyfactor AnyCA Gateway REST **26.2 or later** (DNS validation support was added in AnyCA Gateway 26.2)
- A gateway product that supports DNS-01 domain validation (ACME REST Gateway, DigiCert, Sectigo, etc.)

### LuaDNS Requirements

- A LuaDNS account with one or more DNS zones managed by LuaDNS
- A LuaDNS API key (create under **Account > API Keys** in the LuaDNS dashboard)

### Runtime Requirements
- .NET 10.0 runtime (provided by the gateway server)
- Network connectivity to api.luadns.com (HTTPS/443)

## Installation

This plugin is installed alongside any Keyfactor gateway server that supports DNS-01 domain validation (ACME REST Gateway, DigiCert, Sectigo, etc.). The same DLL works with every supported gateway.

> See the official Keyfactor AnyCA Gateway REST installation documentation for the authoritative install instructions: **<TBD link from Sarah Duncan>**. The steps below are a general guide; defer to the official docs if they diverge.

### 1. Download the Plugin

Download the latest release from the [Releases](https://github.com/Keyfactor/luadns-dnsplugin/releases) page.

### 2. Copy the plugin DLLs to the gateway's Extensions folder

On the server hosting your gateway, unzip the release and copy the contents of the `net10.0` directory into the gateway's `Extensions` folder.

**Windows** (example path — substitute the gateway product folder for your install):

```text
C:\Program Files\Keyfactor\<GatewayName>\AnyGatewayREST\net10.0\Extensions\
```

**Linux**:

```text
/opt/keyfactor/<gateway-name>/AnyGatewayREST/net10.0/Extensions/
```

Replace `<GatewayName>` (or `<gateway-name>` on Linux) with the gateway you are installing into (e.g. `AcmeGwDns`, `DigiCert`, `Sectigo`).

### 3. Restart the gateway service

Restart the AnyGatewayREST Windows service for the gateway you installed the plugin into so the Extensions folder is rescanned.

## Configuration

After installing the plugin DLL into the gateway's Extensions folder, configure a new DNS Provider entry in the AnyCA Gateway REST UI and select **LuaDNS** as the provider type. See the official Keyfactor AnyCA Gateway REST documentation for the canonical UI walkthrough: **<TBD link from Sarah Duncan>**.

### LuaDNS Setup

Create a LuaDNS API key for the account that owns the zones you want the plugin to manage:

1. Log in to the LuaDNS dashboard
2. Navigate to **Account > API Keys**
3. Create a new API key and copy the value — it authenticates alongside your account username (email)

Provide the username and key as `LuaDns_Username` and `LuaDns_ApiKey` in the plugin configuration below.

### Configuration Parameters

| Parameter | Description | Required | Example |
|-----------|-------------|----------|---------|
| `LuaDns_Username` | LuaDNS account username (email address) used for HTTP Basic authentication. | Yes | ` ` |
| `LuaDns_ApiKey` | LuaDNS API key used for HTTP Basic authentication. Found under Account > API Keys in the LuaDNS dashboard. | Yes | ` ` |

### Example Configuration

**Standard configuration:**

```json
{
  "LuaDns_Username": "you@example.com",
  "LuaDns_ApiKey": "your-luadns-api-key"
}
```

## Usage

### Automatic Domain Validation

Once configured, the plugin automatically handles DNS validation during certificate enrollment and renewal:

1. **Record Creation**: Plugin creates a DNS TXT record with the validation challenge
2. **Propagation Wait**: Plugin waits for DNS propagation
3. **Verification**: Plugin verifies the record exists on LuaDNS nameservers
4. **Cleanup**: Plugin deletes the validation record after successful validation

### Zone Discovery

The plugin discovers the appropriate LuaDNS zone for a domain by querying the LuaDNS API for all zones on the account, then matching the record's domain against zone names from most specific (longest) to least specific.

### Testing Connectivity

Test LuaDNS connectivity using `curl` against the API:

```bash
# List zones accessible to the account (validates username/API key)
curl -s -u "you@example.com:$LUADNS_API_KEY" https://api.luadns.com/v1/zones
```

## Troubleshooting

### Common Issues

**Authentication Failures**

Symptom: `401 Unauthorized` listing zones

- Verify the API key has not been revoked in the LuaDNS dashboard
- Confirm `LuaDns_Username` is the account's login email, not a display name

**Zone Not Found**

Symptom: `No LuaDNS zone found for example.com`

- Verify the zone exists and is active in the LuaDNS account
- Confirm the account associated with the API key owns that zone

### Logging

Enable debug logging in the gateway's logging configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "Keyfactor.Extensions.DomainValidator.LuaDns": "Debug"
    }
  }
}
```

### Service Status

Check LuaDNS service status: https://www.luadns.com/

## Support

The LuaDNS DNS Provider plugin is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com.

### Resources

- [LuaDNS Documentation](https://www.luadns.com/api.html)
- [Report Issues](https://github.com/Keyfactor/luadns-dnsplugin/issues)
- [Discussions](https://github.com/Keyfactor/luadns-dnsplugin/discussions)

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor DNS Provider plugins](https://github.com/orgs/Keyfactor/repositories?q=dnsplugin).

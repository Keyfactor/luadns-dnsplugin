### Provider Setup

Create a LuaDNS API key for the account that owns the zones you want the plugin to manage:

1. Log in to the LuaDNS dashboard
2. Navigate to **Account > API Keys**
3. Create a new API key and copy the value — it authenticates alongside your account username (email)

Provide the username and key as `LuaDns_Username` and `LuaDns_ApiKey` in the plugin configuration below.

### Example Configurations

**Standard configuration:**

```json
{
  "LuaDns_Username": "you@example.com",
  "LuaDns_ApiKey": "your-luadns-api-key"
}
```

### Zone Discovery

The plugin discovers the appropriate LuaDNS zone for a domain by querying the LuaDNS API for all zones on the account, then matching the record's domain against zone names from most specific (longest) to least specific.

### Testing Connectivity

Test LuaDNS connectivity using `curl` against the API:

```bash
# List zones accessible to the account (validates username/API key)
curl -s -u "you@example.com:$LUADNS_API_KEY" https://api.luadns.com/v1/zones
```

### Troubleshooting

**Authentication Failures**

Symptom: `401 Unauthorized` listing zones

- Verify the API key has not been revoked in the LuaDNS dashboard
- Confirm `LuaDns_Username` is the account's login email, not a display name

**Zone Not Found**

Symptom: `No LuaDNS zone found for example.com`

- Verify the zone exists and is active in the LuaDNS account
- Confirm the account associated with the API key owns that zone

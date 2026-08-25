## Overview

The LuaDNS Provider plugin enables automated DNS-based domain validation for Keyfactor certificate lifecycle management through LuaDNS. This plugin integrates with the LuaDNS API to automatically create, verify, and delete DNS TXT records required for domain validation during certificate issuance and renewal.

## Features

- HTTP Basic authentication using the account's username (email) and API key
- Automatic zone discovery across all zones on the account, matched by longest domain suffix

## Requirements

- A LuaDNS account with one or more DNS zones managed by LuaDNS
- A LuaDNS API key (create under **Account > API Keys** in the LuaDNS dashboard)

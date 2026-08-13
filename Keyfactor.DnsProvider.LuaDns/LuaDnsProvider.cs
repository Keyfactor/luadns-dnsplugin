// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.DomainValidator.LuaDns
{
    internal class LuaDnsProvider
    {
        private static readonly ILogger _logger = LogHandler.GetClassLogger<LuaDnsProvider>();

        private readonly HttpClient _httpClient;

        private class ZoneData
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }
        }

        private class RecordData
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("zone_id")]
            public int ZoneId { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }

            [JsonPropertyName("ttl")]
            public int TTL { get; set; }
        }

        public LuaDnsProvider(string username, string apiKey)
            : this(username, apiKey, new HttpClientHandler())
        {
        }

        // Internal constructor to allow unit tests to inject a fake HttpMessageHandler.
        internal LuaDnsProvider(string username, string apiKey, HttpMessageHandler handler)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username must not be empty", nameof(username));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("apiKey must not be empty", nameof(apiKey));
            }

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.luadns.com/v1/")
            };

            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{apiKey}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<bool> CreateRecordAsync(string recordName, string value, string recordType)
        {
            _logger.LogDebug("Creating {RecordType} record for {RecordName}", recordType, recordName);

            var (zoneName, zoneId) = await FindZoneForRecordAsync(recordName);

            var fqdn = recordName.TrimEnd('.') + ".";
            var payload = new { name = fqdn, type = recordType, content = value, ttl = 300 };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"zones/{zoneId}/records", content);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "LuaDNS API rejected creation of {RecordType} record '{Fqdn}' in zone '{ZoneName}' ({ZoneId}). Status: {StatusCode}. Response: {Response}",
                    recordType, fqdn, zoneName, zoneId, (int)response.StatusCode, result);

                throw new InvalidOperationException(
                    $"LuaDNS API returned {(int)response.StatusCode} ({response.StatusCode}) creating {recordType} record '{fqdn}' in zone '{zoneName}': {result}");
            }

            _logger.LogInformation(
                "Created {RecordType} record '{Fqdn}' in LuaDNS zone '{ZoneName}'",
                recordType, fqdn, zoneName);
            return true;
        }

        public async Task<bool> DeleteRecordAsync(string recordName, string recordType)
        {
            _logger.LogDebug("Deleting {RecordType} record for {RecordName}", recordType, recordName);

            var (zoneName, zoneId) = await FindZoneForRecordAsync(recordName);

            var fqdn = recordName.TrimEnd('.') + ".";

            var recordsResp = await _httpClient.GetAsync($"zones/{zoneId}/records");
            var recordsBody = await recordsResp.Content.ReadAsStringAsync();
            if (!recordsResp.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "LuaDNS API failed to list records for zone '{ZoneName}'. Status: {StatusCode}. Response: {Response}",
                    zoneName, (int)recordsResp.StatusCode, recordsBody);

                throw new InvalidOperationException(
                    $"LuaDNS API returned {(int)recordsResp.StatusCode} ({recordsResp.StatusCode}) listing records in zone '{zoneName}': {recordsBody}");
            }

            var records = JsonSerializer.Deserialize<RecordData[]>(recordsBody) ?? Array.Empty<RecordData>();
            var match = records.FirstOrDefault(r =>
                string.Equals(r.Type, recordType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((r.Name ?? string.Empty).TrimEnd('.'), fqdn.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                // Nothing to clean up — treat as success so cleanup is idempotent.
                _logger.LogInformation(
                    "No {RecordType} record '{Fqdn}' found in zone '{ZoneName}' to delete; treating cleanup as complete",
                    recordType, fqdn, zoneName);
                return true;
            }

            var deleteResp = await _httpClient.DeleteAsync($"zones/{zoneId}/records/{match.Id}");
            var deleteBody = await deleteResp.Content.ReadAsStringAsync();

            if (!deleteResp.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "LuaDNS API rejected deletion of {RecordType} record '{Fqdn}' ({RecordId}) in zone '{ZoneName}'. Status: {StatusCode}. Response: {Response}",
                    recordType, fqdn, match.Id, zoneName, (int)deleteResp.StatusCode, deleteBody);

                throw new InvalidOperationException(
                    $"LuaDNS API returned {(int)deleteResp.StatusCode} ({deleteResp.StatusCode}) deleting {recordType} record '{fqdn}' in zone '{zoneName}': {deleteBody}");
            }

            _logger.LogInformation(
                "Deleted {RecordType} record '{Fqdn}' in LuaDNS zone '{ZoneName}'",
                recordType, fqdn, zoneName);
            return true;
        }

        /// <summary>
        /// Fetches all zones for the account and resolves the zone that owns the given record
        /// by longest matching name suffix, e.g. for "_acme-challenge.www.example.com" it tries
        /// "www.example.com", then "example.com", etc. against the account's zone names.
        /// </summary>
        private async Task<(string zoneName, string zoneId)> FindZoneForRecordAsync(string recordName)
        {
            if (string.IsNullOrWhiteSpace(recordName))
            {
                throw new ArgumentException("Record name must not be empty", nameof(recordName));
            }

            var response = await _httpClient.GetAsync("zones");
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "LuaDNS zone list request failed. Status: {StatusCode}. Response: {Response}. " +
                    "This usually means the account username or API key is invalid.",
                    (int)response.StatusCode, body);

                throw new InvalidOperationException(
                    $"LuaDNS API returned {(int)response.StatusCode} ({response.StatusCode}) while listing zones: {body}. " +
                    "Verify the configured username and API key.");
            }

            var zones = JsonSerializer.Deserialize<ZoneData[]>(body) ?? Array.Empty<ZoneData>();
            if (zones.Length == 0 || zones.Any(z => z.Name == null))
            {
                throw new InvalidOperationException("LuaDNS returned an empty or invalid zones list. Aborting.");
            }

            var zoneMap = zones.ToDictionary(z => z.Name.TrimEnd('.'), z => z.Id, StringComparer.OrdinalIgnoreCase);
            var match = FindBestMatch(zoneMap, recordName.TrimEnd('.'));

            if (match == null)
            {
                throw new InvalidOperationException(
                    $"No LuaDNS zone found for record '{recordName}'. Ensure the zone exists in this LuaDNS account.");
            }

            return (match.Value.Key, match.Value.Value.ToString());
        }

        /// <summary>
        /// Finds the zone whose name is the longest suffix match of the target domain,
        /// e.g. for domain "_acme-challenge.www.example.com" and zones {"example.com", "com"},
        /// "example.com" wins because it's the more specific (longer) match.
        /// </summary>
        internal static KeyValuePair<string, int>? FindBestMatch(Dictionary<string, int> zones, string domain)
        {
            KeyValuePair<string, int>? best = null;
            foreach (var zone in zones)
            {
                var isMatch = domain.Equals(zone.Key, StringComparison.OrdinalIgnoreCase) ||
                              domain.EndsWith("." + zone.Key, StringComparison.OrdinalIgnoreCase);

                if (isMatch && (best == null || zone.Key.Length > best.Value.Key.Length))
                {
                    best = zone;
                }
            }
            return best;
        }
    }
}

// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0
using Keyfactor.AnyGateway.Extensions;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.DomainValidator.LuaDns
{
    /// <summary>
    /// LuaDNS domain validator for ACME DNS-01 challenges. Publishes TXT records
    /// in LuaDNS-hosted zones. Authenticates via HTTP Basic auth using the
    /// account's username (email) and API key.
    /// </summary>
    public class LuaDnsDomainValidator : IDomainValidator
    {
        private static readonly ILogger _logger = LogHandler.GetClassLogger<LuaDnsDomainValidator>();

        private const string ValidationTypeName = "dns-01";
        private const string RecordTypeName = "TXT";

        private LuaDnsProvider _provider;
        private Dictionary<string, object> _configuration;

        public Dictionary<string, PropertyConfigInfo> GetDomainValidatorAnnotations()
        {
            return new Dictionary<string, PropertyConfigInfo>()
            {
                ["LuaDns_Username"] = new PropertyConfigInfo()
                {
                    Comments = "LuaDNS account username (email address) (Required)",
                    Hidden = false,
                    DefaultValue = "",
                    Type = "String"
                },
                ["LuaDns_ApiKey"] = new PropertyConfigInfo()
                {
                    Comments = "LuaDNS API key (Required)",
                    Hidden = true,
                    DefaultValue = "",
                    Type = "Secret"
                }
            };
        }

        public string GetValidationType() => ValidationTypeName;

        public void Initialize(IDomainValidatorConfigProvider configProvider)
        {
            _configuration = configProvider.DomainValidationConfiguration;

            var username = GetConfigValue("LuaDns_Username");
            var apiKey = GetConfigValue("LuaDns_ApiKey");

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("LuaDns_Username is required");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("LuaDns_ApiKey is required");
            }

            _provider = new LuaDnsProvider(username, apiKey);
        }

        public async Task<DomainValidationResult> StageValidation(string key, string value, CancellationToken cancellationToken)
        {
            try
            {
                var success = await _provider.CreateRecordAsync(key, value, RecordTypeName);

                return new DomainValidationResult
                {
                    Success = success,
                    ErrorMessage = success ? null : $"Failed to create DNS {RecordTypeName} record for {key}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LuaDNS StageValidation failed for {RecordType} record '{Key}'", RecordTypeName, key);
                return new DomainValidationResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create {RecordTypeName} record for {key}: {ex.Message}"
                };
            }
        }

        public async Task<DomainValidationResult> CleanupValidation(string key, CancellationToken cancellationToken)
        {
            try
            {
                var success = await _provider.DeleteRecordAsync(key, RecordTypeName);

                return new DomainValidationResult
                {
                    Success = success,
                    ErrorMessage = success ? null : $"Failed to delete DNS {RecordTypeName} record for {key}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LuaDNS CleanupValidation failed for {RecordType} record '{Key}'", RecordTypeName, key);
                return new DomainValidationResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to delete {RecordTypeName} record for {key}: {ex.Message}"
                };
            }
        }

        public async Task ValidateConfiguration(Dictionary<string, object> configuration)
        {
            _configuration = configuration;

            var username = GetConfigValue("LuaDns_Username");
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("LuaDns_Username is required");
            }

            var apiKey = GetConfigValue("LuaDns_ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("LuaDns_ApiKey is required");
            }

            await Task.CompletedTask;
        }

        private string GetConfigValue(string key)
        {
            if (_configuration != null && _configuration.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }
    }
}

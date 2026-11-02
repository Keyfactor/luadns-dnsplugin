// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0
namespace Keyfactor.Extensions.DomainValidator.LuaDns
{
    /// <summary>
    /// LuaDNS domain validator for ACME DNS-01 challenges. Publishes TXT records
    /// in LuaDNS-hosted zones. Authenticates via HTTP Basic auth using the
    /// account's username (email) and API key.
    /// </summary>
    public class LuaDnsDomainValidator : LuaDnsDomainValidatorBase
    {
        protected override string ValidationTypeName => "dns-01";

        protected override string RecordTypeName => "TXT";
    }
}

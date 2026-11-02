// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0
namespace Keyfactor.Extensions.DomainValidator.LuaDns
{
    /// <summary>
    /// LuaDNS domain validator for CA-driven CNAME domain control validation, as used by
    /// CAs that ask the customer to publish a CNAME (for example CSC Global / Sectigo).
    /// Publishes a CNAME record in the LuaDNS zone that owns the record name. Shares
    /// credentials and zone discovery with <see cref="LuaDnsDomainValidator"/>.
    /// </summary>
    public class LuaDnsCnameDomainValidator : LuaDnsDomainValidatorBase
    {
        protected override string ValidationTypeName => "cname";

        protected override string RecordTypeName => "CNAME";

        /// <summary>
        /// CAs hand over the CNAME target without its trailing dot (the CSC plugin strips it).
        /// LuaDNS reads a hostname without a trailing dot as relative to the zone, which would
        /// turn "x.sectigo.com" into "x.sectigo.com.&lt;zone&gt;", so the target is made absolute.
        /// </summary>
        protected override string NormalizeValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("CNAME target must not be empty", nameof(value));
            }

            return value.Trim().TrimEnd('.') + ".";
        }
    }
}

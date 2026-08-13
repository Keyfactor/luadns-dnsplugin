using Xunit;

namespace Keyfactor.Extensions.DomainValidator.LuaDns.Tests
{
    public class LuaDnsDomainValidatorTests
    {
        [Fact]
        public void GetValidationType_ReturnsDns01()
        {
            var validator = new LuaDnsDomainValidator();

            Assert.Equal("dns-01", validator.GetValidationType());
        }

        [Fact]
        public void GetDomainValidatorAnnotations_DeclaresUsernameAndApiKey()
        {
            var validator = new LuaDnsDomainValidator();

            var annotations = validator.GetDomainValidatorAnnotations();

            Assert.True(annotations.ContainsKey("LuaDns_Username"));
            Assert.True(annotations.ContainsKey("LuaDns_ApiKey"));
            Assert.Equal("Secret", annotations["LuaDns_ApiKey"].Type);
            Assert.True(annotations["LuaDns_ApiKey"].Hidden);
            Assert.False(annotations["LuaDns_Username"].Hidden);
        }

        [Fact]
        public async Task ValidateConfiguration_ThrowsWhenUsernameMissing()
        {
            var validator = new LuaDnsDomainValidator();
            var config = new Dictionary<string, object> { ["LuaDns_ApiKey"] = "key" };

            await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateConfiguration(config));
        }

        [Fact]
        public async Task ValidateConfiguration_ThrowsWhenApiKeyMissing()
        {
            var validator = new LuaDnsDomainValidator();
            var config = new Dictionary<string, object> { ["LuaDns_Username"] = "user@example.com" };

            await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateConfiguration(config));
        }

        [Fact]
        public async Task ValidateConfiguration_SucceedsWhenBothFieldsPresent()
        {
            var validator = new LuaDnsDomainValidator();
            var config = new Dictionary<string, object>
            {
                ["LuaDns_Username"] = "user@example.com",
                ["LuaDns_ApiKey"] = "key"
            };

            await validator.ValidateConfiguration(config);
        }
    }
}

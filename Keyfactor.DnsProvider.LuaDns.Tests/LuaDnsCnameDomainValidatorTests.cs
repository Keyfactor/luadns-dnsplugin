using System.Net;
using System.Text.Json;
using Xunit;

namespace Keyfactor.Extensions.DomainValidator.LuaDns.Tests
{
    public class LuaDnsCnameDomainValidatorTests
    {
        private const string Zones = "[{\"id\":42,\"name\":\"example.com.\"}]";

        [Fact]
        public void GetValidationType_ReturnsCname()
        {
            Assert.Equal("cname", new LuaDnsCnameDomainValidator().GetValidationType());
        }

        [Fact]
        public void TxtValidator_StillAdvertisesDns01()
        {
            // The CNAME validator is a sibling; the original validator must be unchanged.
            Assert.Equal("dns-01", new LuaDnsDomainValidator().GetValidationType());
        }

        [Fact]
        public void GetDomainValidatorAnnotations_SameFieldsAsTxtValidator()
        {
            var annotations = new LuaDnsCnameDomainValidator().GetDomainValidatorAnnotations();

            Assert.True(annotations.ContainsKey("LuaDns_Username"));
            Assert.True(annotations.ContainsKey("LuaDns_ApiKey"));
            Assert.Equal("Secret", annotations["LuaDns_ApiKey"].Type);
            Assert.True(annotations["LuaDns_ApiKey"].Hidden);
        }

        [Theory]
        [InlineData("target.sectigo.com")]
        [InlineData("target.sectigo.com.")]
        public async Task StageValidation_PostsCnameWithAbsoluteTarget(string target)
        {
            HttpRequestMessage post = null;
            string body = null;
            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK, Zones);
                }

                post = req;
                body = req.Content.ReadAsStringAsync().Result;
                return FakeHttpMessageHandler.Json(HttpStatusCode.Created, "{}");
            });
            var validator = new LuaDnsCnameDomainValidator();
            validator.SetProviderForTesting(new LuaDnsProvider("user@example.com", "key", handler));

            var result = await validator.StageValidation("_abc123.example.com", target, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotNull(post);
            Assert.EndsWith("zones/42/records", post.RequestUri.PathAndQuery);
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("CNAME", doc.RootElement.GetProperty("type").GetString());
            Assert.Equal("_abc123.example.com.", doc.RootElement.GetProperty("name").GetString());
            // Always exactly one trailing dot, whether or not the caller supplied one.
            Assert.Equal("target.sectigo.com.", doc.RootElement.GetProperty("content").GetString());
        }

        [Fact]
        public async Task StageValidation_EmptyTarget_ReturnsFailureWithoutCallingApi()
        {
            var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, Zones));
            var validator = new LuaDnsCnameDomainValidator();
            validator.SetProviderForTesting(new LuaDnsProvider("user@example.com", "key", handler));

            var result = await validator.StageValidation("_abc123.example.com", " ", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("CNAME", result.ErrorMessage);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task StageValidation_ApiRejection_ReturnsFailureNotException()
        {
            var handler = new FakeHttpMessageHandler(req =>
                req.Method == HttpMethod.Get
                    ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, Zones)
                    : FakeHttpMessageHandler.Json(HttpStatusCode.UnprocessableEntity, "{\"error\":\"bad\"}"));
            var validator = new LuaDnsCnameDomainValidator();
            validator.SetProviderForTesting(new LuaDnsProvider("user@example.com", "key", handler));

            var result = await validator.StageValidation("_abc123.example.com", "t.sectigo.com", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("422", result.ErrorMessage);
        }

        [Fact]
        public async Task CleanupValidation_DeletesMatchingCnameOnly()
        {
            var deleted = new List<string>();
            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.Method == HttpMethod.Delete)
                {
                    deleted.Add(req.RequestUri.PathAndQuery);
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
                }

                if (req.RequestUri.PathAndQuery.EndsWith("zones/42/records"))
                {
                    // A TXT at the same name must survive a CNAME cleanup.
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                        "[{\"id\":1,\"zone_id\":42,\"name\":\"_abc123.example.com.\",\"type\":\"TXT\",\"content\":\"x\",\"ttl\":300}," +
                        "{\"id\":2,\"zone_id\":42,\"name\":\"_abc123.example.com.\",\"type\":\"CNAME\",\"content\":\"t.sectigo.com.\",\"ttl\":300}]");
                }

                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, Zones);
            });
            var validator = new LuaDnsCnameDomainValidator();
            validator.SetProviderForTesting(new LuaDnsProvider("user@example.com", "key", handler));

            var result = await validator.CleanupValidation("_abc123.example.com", CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Single(deleted);
            Assert.EndsWith("zones/42/records/2", deleted[0]);
        }

        [Fact]
        public async Task CleanupValidation_RecordAlreadyGone_IsSuccess()
        {
            var handler = new FakeHttpMessageHandler(req =>
                req.RequestUri.PathAndQuery.EndsWith("zones/42/records")
                    ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]")
                    : FakeHttpMessageHandler.Json(HttpStatusCode.OK, Zones));
            var validator = new LuaDnsCnameDomainValidator();
            validator.SetProviderForTesting(new LuaDnsProvider("user@example.com", "key", handler));

            var result = await validator.CleanupValidation("_abc123.example.com", CancellationToken.None);

            Assert.True(result.Success);
        }

        [Fact]
        public void Manifest_RegistersBothValidators()
        {
            // manifest.json is copied next to the test assembly via the main project's output.
            var path = Path.Combine(AppContext.BaseDirectory, "manifest.json");
            Assert.True(File.Exists(path), $"manifest.json not found at {path}");

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var validators = doc.RootElement
                .GetProperty("extensions")
                .GetProperty("Keyfactor.AnyGateway.Extensions.IDomainValidator");

            foreach (var (key, type) in new[]
            {
                ("LuaDnsDomainValidator", typeof(LuaDnsDomainValidator)),
                ("LuaDnsCnameDomainValidator", typeof(LuaDnsCnameDomainValidator))
            })
            {
                var entry = validators.GetProperty(key);
                Assert.Equal(type.FullName, entry.GetProperty("TypeFullName").GetString());
                Assert.Equal(type.Assembly.GetName().Name + ".dll", entry.GetProperty("assemblypath").GetString());
            }
        }
    }
}

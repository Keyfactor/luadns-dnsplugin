using System.Net;
using Xunit;

namespace Keyfactor.Extensions.DomainValidator.LuaDns.Tests
{
    public class LuaDnsProviderTests
    {
        [Fact]
        public void FindBestMatch_PicksLongestMatchingSuffix()
        {
            var zones = new Dictionary<string, int>
            {
                ["com"] = 1,
                ["example.com"] = 2,
                ["other.com"] = 3
            };

            var match = LuaDnsProvider.FindBestMatch(zones, "_acme-challenge.www.example.com");

            Assert.NotNull(match);
            Assert.Equal("example.com", match.Value.Key);
            Assert.Equal(2, match.Value.Value);
        }

        [Fact]
        public void FindBestMatch_MatchesExactZoneName()
        {
            var zones = new Dictionary<string, int> { ["example.com"] = 2 };

            var match = LuaDnsProvider.FindBestMatch(zones, "example.com");

            Assert.NotNull(match);
            Assert.Equal("example.com", match.Value.Key);
        }

        [Fact]
        public void FindBestMatch_ReturnsNullWhenNoZoneMatches()
        {
            var zones = new Dictionary<string, int> { ["example.com"] = 2 };

            var match = LuaDnsProvider.FindBestMatch(zones, "unrelated-domain.net");

            Assert.Null(match);
        }

        [Fact]
        public void FindBestMatch_DoesNotMatchUnrelatedSuffixSubstring()
        {
            // "notexample.com" must not match zone "example.com" just because it ends with the same characters.
            var zones = new Dictionary<string, int> { ["example.com"] = 2 };

            var match = LuaDnsProvider.FindBestMatch(zones, "notexample.com");

            Assert.Null(match);
        }

        [Fact]
        public async Task CreateRecordAsync_PostsTxtRecordToResolvedZone()
        {
            HttpRequestMessage postRequest = null;

            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri.PathAndQuery.EndsWith("zones"))
                {
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                        "[{\"id\":42,\"name\":\"example.com.\"}]");
                }

                if (req.Method == HttpMethod.Post)
                {
                    postRequest = req;
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                        "{\"id\":1,\"zone_id\":42,\"name\":\"_acme-challenge.example.com.\",\"type\":\"TXT\",\"content\":\"abc123\",\"ttl\":300}");
                }

                throw new InvalidOperationException($"Unexpected request: {req.Method} {req.RequestUri}");
            });

            var provider = new LuaDnsProvider("user@example.com", "apikey", handler);

            var result = await provider.CreateRecordAsync("_acme-challenge.example.com", "abc123", "TXT");

            Assert.True(result);
            Assert.NotNull(postRequest);
            Assert.EndsWith("zones/42/records", postRequest.RequestUri.PathAndQuery);

            var body = await postRequest.Content.ReadAsStringAsync();
            Assert.Contains("\"name\":\"_acme-challenge.example.com.\"", body);
            Assert.Contains("\"type\":\"TXT\"", body);
            Assert.Contains("\"content\":\"abc123\"", body);
        }

        [Fact]
        public async Task CreateRecordAsync_ThrowsWithApiDetailsWhenZoneApiRejects()
        {
            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri.PathAndQuery.EndsWith("zones"))
                {
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                        "[{\"id\":42,\"name\":\"example.com.\"}]");
                }

                return FakeHttpMessageHandler.Json(HttpStatusCode.BadRequest, "{\"error\":\"invalid content\"}");
            });

            var provider = new LuaDnsProvider("user@example.com", "apikey", handler);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.CreateRecordAsync("_acme-challenge.example.com", "abc123", "TXT"));

            Assert.Contains("400", ex.Message);
            Assert.Contains("example.com", ex.Message);
        }

        [Fact]
        public async Task CreateRecordAsync_ThrowsWhenNoZoneMatches()
        {
            var handler = new FakeHttpMessageHandler(req =>
                FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[{\"id\":42,\"name\":\"other.com.\"}]"));

            var provider = new LuaDnsProvider("user@example.com", "apikey", handler);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.CreateRecordAsync("_acme-challenge.example.com", "abc123", "TXT"));

            Assert.Contains("No LuaDNS zone found", ex.Message);
        }

        [Fact]
        public async Task DeleteRecordAsync_DeletesMatchingRecord()
        {
            HttpRequestMessage deleteRequest = null;

            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri.PathAndQuery.EndsWith("zones"))
                {
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                        "[{\"id\":42,\"name\":\"example.com.\"}]");
                }

                if (req.Method == HttpMethod.Get && req.RequestUri.PathAndQuery.Contains("records"))
                {
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                        "[{\"id\":7,\"zone_id\":42,\"name\":\"_acme-challenge.example.com.\",\"type\":\"TXT\",\"content\":\"abc123\",\"ttl\":300}]");
                }

                if (req.Method == HttpMethod.Delete)
                {
                    deleteRequest = req;
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
                }

                throw new InvalidOperationException($"Unexpected request: {req.Method} {req.RequestUri}");
            });

            var provider = new LuaDnsProvider("user@example.com", "apikey", handler);

            var result = await provider.DeleteRecordAsync("_acme-challenge.example.com", "TXT");

            Assert.True(result);
            Assert.NotNull(deleteRequest);
            Assert.EndsWith("zones/42/records/7", deleteRequest.RequestUri.PathAndQuery);
        }

        [Fact]
        public async Task DeleteRecordAsync_IsIdempotentWhenRecordAlreadyGone()
        {
            var handler = new FakeHttpMessageHandler(req =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri.PathAndQuery.EndsWith("zones"))
                {
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                        "[{\"id\":42,\"name\":\"example.com.\"}]");
                }

                if (req.Method == HttpMethod.Get && req.RequestUri.PathAndQuery.Contains("records"))
                {
                    return FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
                }

                throw new InvalidOperationException($"Unexpected request: {req.Method} {req.RequestUri}");
            });

            var provider = new LuaDnsProvider("user@example.com", "apikey", handler);

            var result = await provider.DeleteRecordAsync("_acme-challenge.example.com", "TXT");

            Assert.True(result);
        }

        [Theory]
        [InlineData(null, "apikey")]
        [InlineData("", "apikey")]
        [InlineData("user@example.com", null)]
        [InlineData("user@example.com", "")]
        public void Constructor_ThrowsOnMissingCredentials(string username, string apiKey)
        {
            Assert.Throws<ArgumentException>(() => new LuaDnsProvider(username, apiKey, new FakeHttpMessageHandler(_ =>
                throw new InvalidOperationException("Should not make HTTP calls"))));
        }
    }
}

using Graviton.Models;

using System.Text;

using Xunit;

namespace Graviton.Tests.Http
{
    [Collection(GravitonCollection.Name)]
    public class HttpClientAuthTests(GravitonTestFixture fixture)
    {
        private static string DecodeBasicParameter(string base64Parameter) => Encoding.UTF8.GetString(Convert.FromBase64String(base64Parameter));

        #region ConfigureBasicAuth

        [Fact]
        public void ConfigureBasicAuth_SetsAuthToBasic()
        {
            HttpClientSingleton.ConfigureBasicAuth("user", "pass");

            var auth = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization;
            Assert.NotNull(auth);
            Assert.Equal("Basic", auth!.Scheme);
        }

        [Fact]
        public void ConfigureBasicAuth_HeaderAuthIsFormatedCorrectly()
        {
            HttpClientSingleton.ConfigureBasicAuth("alice", "s3cr3t");

            var parameter = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!.Parameter!;

            Assert.Equal("alice:s3cr3t", DecodeBasicParameter(parameter));
        }

        [Fact]
        public void ConfigureBasicAuth_SpecialChararcterAreEncoded()
        {
            const string user = "admin";
            const string pass = "p@$$w0rd!#%";

            HttpClientSingleton.ConfigureBasicAuth(user, pass);

            var parameter = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!.Parameter!;

            Assert.Equal($"{user}:{pass}", DecodeBasicParameter(parameter));
        }

        [Fact]
        public void ConfigureBasicAuth_ClearsPreviousClientAuthHeader()
        {
            HttpClientSingleton.ConfigureClientToken("old-token");
            HttpClientSingleton.ConfigureBasicAuth("bob", "hunter2");

            var auth = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!;
            Assert.Equal("Basic", auth.Scheme);
        }

        [Fact]
        public void ConfigureBasicAuth_EnabledCustomHeader_IsAddedToDefaultHeaders()
        {
            fixture.ApplySettings(s => s.CustomHeaders.Add(new CustomHTTPHeader { Enabled = true, Name = "X-Api-Key", Value = "abc123" }));

            try
            {
                HttpClientSingleton.ConfigureBasicAuth("user", "pass");

                Assert.Contains("abc123", HttpClientSingleton.Instance.DefaultRequestHeaders.GetValues("X-Api-Key"));
            }
            finally
            {
                HttpClientSingleton.Instance.DefaultRequestHeaders.Remove("X-Api-Key");
            }
        }

        [Fact]
        public void ConfigureBasicAuth_DisabledCustomHeader_IsNotAddedToDefaultHeaders()
        {
            fixture.ApplySettings(s => s.CustomHeaders.Add(new CustomHTTPHeader { Enabled = false, Name = "X-Disabled-Header", Value = "shouldnotappear" }));

            HttpClientSingleton.ConfigureBasicAuth("user", "pass");

            Assert.False(HttpClientSingleton.Instance.DefaultRequestHeaders.Contains("X-Disabled-Header"));
        }

        #endregion

        #region ConfigureClientToken

        [Fact]
        public void ConfigureClientToken_SetsAuthToBearer()
        {
            HttpClientSingleton.ConfigureClientToken("rmm_mytoken");

            var auth = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization;
            Assert.NotNull(auth);
            Assert.Equal("Bearer", auth!.Scheme);
        }

        [Fact]
        public void ConfigureClientToken_AuthHeaderGetsToken()
        {
            const string token = "rmm_eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";

            HttpClientSingleton.ConfigureClientToken(token);

            Assert.Equal(token, HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!.Parameter);
        }

        [Fact]
        public void ConfigureClientToken_ClearsPreviousBasicAuthHeader()
        {
            HttpClientSingleton.ConfigureBasicAuth("user", "pass");
            HttpClientSingleton.ConfigureClientToken("new-token");

            var auth = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!;
            Assert.Equal("Bearer", auth.Scheme);
            Assert.Equal("new-token", auth.Parameter);
        }

        [Fact]
        public void ConfigureClientToken_EnabledCustomHeader_IsAddedToDefaultHeaders()
        {
            fixture.ApplySettings(s => s.CustomHeaders.Add(new CustomHTTPHeader { Enabled = true, Name = "X-Client-Id", Value = "device-9" }));

            try
            {
                HttpClientSingleton.ConfigureClientToken("mytoken");

                Assert.Contains("device-9", HttpClientSingleton.Instance.DefaultRequestHeaders.GetValues("X-Client-Id"));
            }
            finally
            {
                HttpClientSingleton.Instance.DefaultRequestHeaders.Remove("X-Client-Id");
            }
        }

        [Fact]
        public void ConfigureClientToken_ReConfiguredCustomHeaderValue_ReplacesPreviousValue()
        {
            fixture.ApplySettings(s => s.CustomHeaders.Add(new CustomHTTPHeader { Enabled = true, Name = "X-Client-Id", Value = "first-value" }));
            HttpClientSingleton.ConfigureClientToken("mytoken");

            fixture.ApplySettings(s => s.CustomHeaders.Add(new CustomHTTPHeader { Enabled = true, Name = "X-Client-Id", Value = "second-value" }));

            try
            {
                HttpClientSingleton.ConfigureClientToken("mytoken");

                var values = HttpClientSingleton.Instance.DefaultRequestHeaders.GetValues("X-Client-Id");
                Assert.Single(values);
                Assert.Equal("second-value", values.First());
            }
            finally
            {
                HttpClientSingleton.Instance.DefaultRequestHeaders.Remove("X-Client-Id");
            }
        }

        #endregion

        #region Instance

        [Fact]
        public void Instance_DefaultAcceptHeader_IncludesApplicationJson()
        {
            Assert.Contains(HttpClientSingleton.Instance.DefaultRequestHeaders.Accept, h => h.MediaType == "application/json");
        }

        [Fact]
        public void Instance_DefaultTimeout_Is30Seconds()
        {
            Assert.Equal(TimeSpan.FromSeconds(30), HttpClientSingleton.Instance.Timeout);
        }

        #endregion
    }
}

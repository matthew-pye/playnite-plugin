using System.Text;

using Xunit;

namespace Graviton.Tests.Http
{
    [Collection(GravitonCollection.Name)]
    public class HttpClientAuthTests
    {
        #region ConfigureBasicAuth

        [Fact]
        public void ConfigureBasicAuth_SetsSchemeToBasic()
        {
            HttpClientSingleton.ConfigureBasicAuth("user", "pass");

            var auth = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization;
            Assert.NotNull(auth);
            Assert.Equal("Basic", auth!.Scheme);
        }

        [Fact]
        public void ConfigureBasicAuth_ParameterIsUtf8Base64OfUserColonPassword()
        {
            HttpClientSingleton.ConfigureBasicAuth("alice", "s3cr3t");

            var parameter = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!.Parameter!;
            var decoded   = Encoding.UTF8.GetString(Convert.FromBase64String(parameter));

            Assert.Equal("alice:s3cr3t", decoded);
        }

        [Fact]
        public void ConfigureBasicAuth_SpecialCharactersInCredentials_EncodedCorrectly()
        {
            const string user = "admin";
            const string pass = "p@$$w0rd!#%";

            HttpClientSingleton.ConfigureBasicAuth(user, pass);

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!.Parameter!));
            Assert.Equal($"{user}:{pass}", decoded);
        }

        [Fact]
        public void ConfigureBasicAuth_OverwritesPreviousBearerToken()
        {
            HttpClientSingleton.ConfigureClientToken("old-token");
            HttpClientSingleton.ConfigureBasicAuth("bob", "hunter2");

            var auth = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!;
            Assert.Equal("Basic", auth.Scheme);
        }

        #endregion

        #region ConfigureClientTokenAuth

        [Fact]
        public void ConfigureClientToken_SetsSchemeToBearer()
        {
            HttpClientSingleton.ConfigureClientToken("mytoken");

            var auth = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization;
            Assert.NotNull(auth);
            Assert.Equal("Bearer", auth!.Scheme);
        }

        [Fact]
        public void ConfigureClientToken_ParameterMatchesProvidedToken()
        {
            const string token = "rmm_eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";

            HttpClientSingleton.ConfigureClientToken(token);

            Assert.Equal(token, HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!.Parameter);
        }

        [Fact]
        public void ConfigureClientToken_OverwritesPreviousBasicAuth()
        {
            HttpClientSingleton.ConfigureBasicAuth("user", "pass");
            HttpClientSingleton.ConfigureClientToken("new-token");

            var auth = HttpClientSingleton.Instance.DefaultRequestHeaders.Authorization!;
            Assert.Equal("Bearer",    auth.Scheme);
            Assert.Equal("new-token", auth.Parameter);
        }

        #endregion

        [Fact]
        public void Instance_DefaultAcceptHeader_IncludesApplicationJson()
        {
            Assert.Contains(HttpClientSingleton.Instance.DefaultRequestHeaders.Accept, h => h.MediaType == "application/json");
        }
    }
}

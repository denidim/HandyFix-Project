namespace HandyFix.Web.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Primitives;

    using Moq;

    using Xunit;

    // Cloudflare Turnstile, the "are you a person" check on the public forms (PROJECT_STATE.md
    // Section 3cb). Cloudflare itself is played by a handler that answers as told.
    public class TurnstileVerifierTests
    {
        private const string RealLookingSecret = "0x4AAAAAAA-a-secret-of-the-real-shape";

        // No keys on a developer's machine: the check is off, nothing is shown and nothing is asked.
        [Fact]
        public async Task WithNoKeysInDevelopmentTheCheckIsOff()
        {
            var cloudflare = new FakeCloudflare();
            TurnstileVerifier verifier = Build(cloudflare, Environments.Development, siteKey: null, secretKey: null);

            Assert.Null(verifier.SiteKey);
            Assert.True(await verifier.VerifyAsync(Submission(token: null), FormNames.Contact));
            Assert.Equal(0, cloudflare.Calls);
        }

        // A deployed site with no keys must not quietly run its forms unprotected. It fails,
        // where it is seen: when a form is shown and when one is sent.
        [Theory]
        [InlineData("Staging")]
        [InlineData("Production")]
        public async Task WithNoKeysOutsideDevelopmentItFailsLoudly(string environment)
        {
            TurnstileVerifier verifier = Build(new FakeCloudflare(), environment, siteKey: null, secretKey: null);

            Assert.Contains("Turnstile is not configured", Assert.Throws<InvalidOperationException>(() => verifier.SiteKey).Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => verifier.VerifyAsync(Submission("token"), FormNames.Contact));
        }

        [Theory]
        [InlineData("site-key", null)]
        [InlineData(null, "secret-key")]
        [InlineData("site-key", "  ")]
        public void WithOneKeyAndNotTheOtherItFailsLoudlyEverywhere(string siteKey, string secretKey)
        {
            TurnstileVerifier verifier = Build(new FakeCloudflare(), Environments.Development, siteKey, secretKey);

            Assert.Contains("half configured", Assert.Throws<InvalidOperationException>(() => verifier.SiteKey).Message);
        }

        [Fact]
        public async Task AGoodTokenForThisFormOnThisSitePasses()
        {
            var cloudflare = new FakeCloudflare("{\"success\":true,\"action\":\"contact\",\"hostname\":\"plumbing-handyman-surrey.co.uk\",\"error-codes\":[]}");
            TurnstileVerifier verifier = Build(cloudflare);

            Assert.Equal("site-key", verifier.SiteKey);
            Assert.True(await verifier.VerifyAsync(Submission("the-token"), FormNames.Contact));

            // Cloudflare was asked, with the secret, the token and the visitor's address.
            Assert.Equal(1, cloudflare.Calls);
            Assert.Equal("https://challenges.cloudflare.com/turnstile/v0/siteverify", cloudflare.LastUrl);
            Assert.Contains("secret=" + Uri.EscapeDataString(RealLookingSecret), cloudflare.LastBody);
            Assert.Contains("response=the-token", cloudflare.LastBody);
            Assert.Contains("remoteip=203.0.113.7", cloudflare.LastBody);
        }

        // The widget had not finished, or was blocked, or was never there: there is no token to
        // ask about, so Cloudflare is not asked.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ASubmissionWithNoTokenFailsWithoutAskingCloudflare(string token)
        {
            var cloudflare = new FakeCloudflare();
            TurnstileVerifier verifier = Build(cloudflare);

            Assert.False(await verifier.VerifyAsync(Submission(token), FormNames.Contact));
            Assert.Equal(0, cloudflare.Calls);
        }

        [Fact]
        public async Task ATokenLongerThanCloudflareMakesFailsWithoutAskingCloudflare()
        {
            var cloudflare = new FakeCloudflare();

            Assert.False(await Build(cloudflare).VerifyAsync(Submission(new string('x', 2049)), FormNames.Contact));
            Assert.Equal(0, cloudflare.Calls);
        }

        [Theory]
        [InlineData("{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}")]
        [InlineData("{\"success\":false,\"error-codes\":[\"timeout-or-duplicate\"]}")]
        [InlineData("{\"success\":false,\"error-codes\":[\"invalid-input-secret\"]}")]
        [InlineData("{\"success\":false}")]
        [InlineData("null")]
        public async Task ATokenCloudflareTurnsDownFails(string answer)
        {
            Assert.False(await Build(new FakeCloudflare(answer)).VerifyAsync(Submission("the-token"), FormNames.Contact));
        }

        // A token says which form it was earned on, and where. One earned on the Contact form
        // cannot be spent on a booking, and one lifted from another site is not good here.
        [Theory]
        [InlineData("{\"success\":true,\"action\":\"booking\",\"hostname\":\"plumbing-handyman-surrey.co.uk\"}")]
        [InlineData("{\"success\":true,\"action\":\"contact\",\"hostname\":\"evil.example\"}")]
        [InlineData("{\"success\":true,\"hostname\":\"plumbing-handyman-surrey.co.uk\"}")]
        public async Task AGoodTokenForAnotherFormOrAnotherSiteFails(string answer)
        {
            Assert.False(await Build(new FakeCloudflare(answer)).VerifyAsync(Submission("the-token"), FormNames.Contact));
        }

        // Cloudflare's published test secrets answer with a made-up hostname and action, so with
        // one of those only "success" is read. This is what local runs and staging start with.
        [Theory]
        [InlineData("1x0000000000000000000000000000000AA", "{\"success\":true,\"action\":\"test\",\"hostname\":\"example.com\"}", true)]
        [InlineData("2x0000000000000000000000000000000AA", "{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}", false)]
        [InlineData("3x0000000000000000000000000000000AA", "{\"success\":false,\"error-codes\":[\"timeout-or-duplicate\"]}", false)]
        public async Task WithATestSecretOnlySuccessIsRead(string testSecret, string answer, bool expected)
        {
            TurnstileVerifier verifier = Build(new FakeCloudflare(answer), secretKey: testSecret);

            Assert.Equal(expected, await verifier.VerifyAsync(Submission("XXXX.DUMMY.TOKEN.XXXX"), FormNames.Contact));
        }

        // Cloudflare cannot be asked: down, slow, or answering with something that is not an
        // answer. That is not the visitor's doing, and refusing every enquiry and booking until
        // it is back would cost more than what the other checks let through meanwhile.
        [Fact]
        public async Task WhenCloudflareCannotBeReachedTheSubmissionIsLetThrough()
        {
            Assert.True(await Build(new FakeCloudflare(new HttpRequestException("No such host is known."))).VerifyAsync(Submission("the-token"), FormNames.Contact));
            Assert.True(await Build(new FakeCloudflare(new TaskCanceledException("The request timed out."))).VerifyAsync(Submission("the-token"), FormNames.Contact));
            Assert.True(await Build(new FakeCloudflare("upstream error", HttpStatusCode.BadGateway)).VerifyAsync(Submission("the-token"), FormNames.Contact));
            Assert.True(await Build(new FakeCloudflare("<html>not json</html>")).VerifyAsync(Submission("the-token"), FormNames.Contact));
        }

        private static TurnstileVerifier Build(
            FakeCloudflare cloudflare,
            string environment = "Production",
            string siteKey = "site-key",
            string secretKey = RealLookingSecret)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    [TurnstileVerifier.SiteKeyKey] = siteKey,
                    [TurnstileVerifier.SecretKeyKey] = secretKey,
                })
                .Build();

            var hostEnvironment = new Mock<IHostEnvironment>();
            hostEnvironment.SetupGet(e => e.EnvironmentName).Returns(environment);

            return new TurnstileVerifier(new HttpClient(cloudflare), configuration, hostEnvironment.Object, NullLogger<TurnstileVerifier>.Instance);
        }

        private static HttpContext Submission(string token)
        {
            var fields = new Dictionary<string, StringValues>();
            if (token != null)
            {
                fields[TurnstileVerifier.ResponseFieldName] = token;
            }

            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Post;
            context.Request.Host = new HostString("plumbing-handyman-surrey.co.uk");
            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Request.Form = new FormCollection(fields);
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            return context;
        }

        private sealed class FakeCloudflare : HttpMessageHandler
        {
            private readonly string answer;
            private readonly HttpStatusCode status;
            private readonly Exception failure;

            public FakeCloudflare(string answer = "{\"success\":true}", HttpStatusCode status = HttpStatusCode.OK)
            {
                this.answer = answer;
                this.status = status;
            }

            public FakeCloudflare(Exception failure)
            {
                this.failure = failure;
            }

            public int Calls { get; private set; }

            public string LastUrl { get; private set; }

            public string LastBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.Calls++;
                this.LastUrl = request.RequestUri.ToString();
                this.LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

                if (this.failure != null)
                {
                    throw this.failure;
                }

                return new HttpResponseMessage(this.status) { Content = new StringContent(this.answer, Encoding.UTF8, "application/json") };
            }
        }
    }
}

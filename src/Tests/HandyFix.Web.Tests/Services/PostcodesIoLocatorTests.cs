namespace HandyFix.Web.Tests.Services
{
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using HandyFix.Services;

    using Microsoft.Extensions.Logging.Abstractions;

    using Xunit;

    // The lookup that places a new area on the coverage diagram (PROJECT_STATE.md Section 3dm).
    // Whatever postcodes.io answers, it gives a place or nothing, and never stops the area saving.
    public class PostcodesIoLocatorTests
    {
        [Fact]
        public async Task LocateDistrictAsyncShouldReadThePlaceFromTheAnswer()
        {
            var postcodesIo = new FakePostcodesIo(HttpStatusCode.OK, "{\"status\":200,\"result\":{\"outcode\":\"KT22\",\"latitude\":51.302,\"longitude\":-0.3396}}");

            GeoPoint? place = await Locator(postcodesIo).LocateDistrictAsync("KT22");

            Assert.Equal(51.302, place.Value.Latitude);
            Assert.Equal(-0.3396, place.Value.Longitude);
            Assert.Equal("https://api.postcodes.io/outcodes/KT22", postcodesIo.AskedFor);
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound, "{\"status\":404,\"error\":\"Outcode not found\"}")]
        [InlineData(HttpStatusCode.OK, "{\"status\":200,\"result\":{\"outcode\":\"KT99\",\"latitude\":null,\"longitude\":null}}")]
        [InlineData(HttpStatusCode.InternalServerError, "")]
        [InlineData(HttpStatusCode.OK, "not json")]
        public async Task LocateDistrictAsyncShouldGiveNothingWhenThereIsNoPlace(HttpStatusCode status, string body)
        {
            GeoPoint? place = await Locator(new FakePostcodesIo(status, body)).LocateDistrictAsync("KT99");

            Assert.Null(place);
        }

        [Fact]
        public async Task LocateDistrictAsyncShouldGiveNothingWhenPostcodesIoCannotBeReached()
        {
            GeoPoint? place = await Locator(new FakePostcodesIo(null, null)).LocateDistrictAsync("KT22");

            Assert.Null(place);
        }

        private static PostcodesIoLocator Locator(FakePostcodesIo postcodesIo) =>
            new PostcodesIoLocator(new HttpClient(postcodesIo), NullLogger<PostcodesIoLocator>.Instance);

        private sealed class FakePostcodesIo : HttpMessageHandler
        {
            private readonly HttpStatusCode? status;
            private readonly string body;

            // No status: the request fails the way an unreachable server does.
            public FakePostcodesIo(HttpStatusCode? status, string body)
            {
                this.status = status;
                this.body = body;
            }

            public string AskedFor { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.AskedFor = request.RequestUri.ToString();
                if (this.status == null)
                {
                    throw new HttpRequestException("No route to host");
                }

                return Task.FromResult(new HttpResponseMessage(this.status.Value)
                {
                    Content = new StringContent(this.body, Encoding.UTF8, "application/json"),
                });
            }
        }
    }
}

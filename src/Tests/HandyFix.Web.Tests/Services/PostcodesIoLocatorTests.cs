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

    // The lookups that place a new area on the coverage diagram (PROJECT_STATE.md Section 3dm) and
    // find a town's district from its name (Section 3dn). Whatever postcodes.io answers, they give
    // an answer or nothing, and never throw.
    public class PostcodesIoLocatorTests
    {
        private static readonly GeoPoint Chessington = new GeoPoint(51.3638, -0.3033);

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

        // A town found by its name fills an empty districts box (PROJECT_STATE.md Section 3dn). A
        // place called exactly that wins over a nearer one whose name only contains it, and of
        // several the nearest to the base: "Ham" is the one in Richmond, not Hampton Court or the
        // one in Somerset.
        [Fact]
        public async Task FindTownDistrictAsyncShouldPreferTheExactNameThenTheNearest()
        {
            string answer = "{\"status\":200,\"result\":["
                + "{\"name_1\":\"Ham\",\"outcode\":\"TA21\",\"latitude\":50.95,\"longitude\":-3.25},"
                + "{\"name_1\":\"Hampton Court\",\"outcode\":\"KT8\",\"latitude\":51.403,\"longitude\":-0.338},"
                + "{\"name_1\":\"Ham\",\"outcode\":\"tw10\",\"latitude\":51.433,\"longitude\":-0.311}]}";
            var postcodesIo = new FakePostcodesIo(HttpStatusCode.OK, answer);

            string district = await Locator(postcodesIo).FindTownDistrictAsync(" Ham ", Chessington);

            Assert.Equal("TW10", district);
            Assert.Equal("https://api.postcodes.io/places?q=Ham&limit=100", postcodesIo.AskedFor);
        }

        // No place called exactly "Molesey": West and East Molesey both count, and the nearest wins.
        [Fact]
        public async Task FindTownDistrictAsyncShouldTakeTheNearestWhenNoNameIsExact()
        {
            string answer = "{\"status\":200,\"result\":["
                + "{\"name_1\":\"Molesey Lock Farm\",\"outcode\":\"GL7\",\"latitude\":51.7,\"longitude\":-1.8},"
                + "{\"name_1\":\"West Molesey\",\"outcode\":\"KT8\",\"latitude\":51.403,\"longitude\":-0.371}]}";
            var postcodesIo = new FakePostcodesIo(HttpStatusCode.OK, answer);

            Assert.Equal("KT8", await Locator(postcodesIo).FindTownDistrictAsync("Molesey", Chessington));
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, "{\"status\":200,\"result\":[]}")]
        [InlineData(HttpStatusCode.OK, "{\"status\":200,\"result\":null}")]
        [InlineData(HttpStatusCode.OK, "{\"status\":200,\"result\":[{\"name_1\":\"Nowhere\",\"outcode\":null,\"latitude\":51.4,\"longitude\":-0.3}]}")]
        [InlineData(HttpStatusCode.InternalServerError, "")]
        [InlineData(HttpStatusCode.OK, "not json")]
        public async Task FindTownDistrictAsyncShouldGiveNothingWhenNoTownIsFound(HttpStatusCode status, string body)
        {
            Assert.Null(await Locator(new FakePostcodesIo(status, body)).FindTownDistrictAsync("Xqzzyton", Chessington));
        }

        [Fact]
        public async Task FindTownDistrictAsyncShouldGiveNothingWhenPostcodesIoCannotBeReached()
        {
            Assert.Null(await Locator(new FakePostcodesIo(null, null)).FindTownDistrictAsync("Molesey", Chessington));
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

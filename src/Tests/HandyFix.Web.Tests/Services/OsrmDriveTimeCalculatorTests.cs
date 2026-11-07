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

    // The drive time worked out when an admin adds a town (PROJECT_STATE.md Section 3dn). Whatever
    // the route service answers, it gives whole minutes or nothing, and never throws: nothing
    // makes the area estimate the time from the distance instead.
    public class OsrmDriveTimeCalculatorTests
    {
        private static readonly GeoPoint Chessington = new GeoPoint(51.3638, -0.3033);
        private static readonly GeoPoint EastMolesey = new GeoPoint(51.4002, -0.355);

        [Fact]
        public async Task DriveMinutesAsyncShouldReadTheRoutesDurationInWholeMinutes()
        {
            var osrm = new FakeOsrm(HttpStatusCode.OK, "{\"code\":\"Ok\",\"routes\":[{\"duration\":698.3,\"distance\":8080.5}]}");

            int? minutes = await Calculator(osrm).DriveMinutesAsync(Chessington, EastMolesey);

            Assert.Equal(12, minutes);

            // Longitude first, with a point for the decimals whatever the server's language.
            Assert.Equal("https://router.project-osrm.org/route/v1/driving/-0.3033,51.3638;-0.355,51.4002?overview=false", osrm.AskedFor);
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, "{\"code\":\"NoRoute\",\"routes\":[]}")]
        [InlineData(HttpStatusCode.OK, "{\"code\":\"Ok\",\"routes\":[]}")]
        [InlineData(HttpStatusCode.BadRequest, "{\"code\":\"InvalidQuery\"}")]
        [InlineData(HttpStatusCode.TooManyRequests, "")]
        [InlineData(HttpStatusCode.OK, "not json")]
        public async Task DriveMinutesAsyncShouldGiveNothingWhenThereIsNoRoute(HttpStatusCode status, string body)
        {
            Assert.Null(await Calculator(new FakeOsrm(status, body)).DriveMinutesAsync(Chessington, EastMolesey));
        }

        [Fact]
        public async Task DriveMinutesAsyncShouldGiveNothingWhenTheServiceCannotBeReached()
        {
            Assert.Null(await Calculator(new FakeOsrm(null, null)).DriveMinutesAsync(Chessington, EastMolesey));
        }

        private static OsrmDriveTimeCalculator Calculator(FakeOsrm osrm) =>
            new OsrmDriveTimeCalculator(new HttpClient(osrm), NullLogger<OsrmDriveTimeCalculator>.Instance);

        private sealed class FakeOsrm : HttpMessageHandler
        {
            private readonly HttpStatusCode? status;
            private readonly string body;

            // No status: the request fails the way an unreachable server does.
            public FakeOsrm(HttpStatusCode? status, string body)
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

namespace HandyFix.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Works out a drive time with the public OSRM route service (router.project-osrm.org): free,
    /// no key or account, built on OpenStreetMap's roads, without traffic. Asked once when an
    /// admin creates an area (PROJECT_STATE.md Section 3dn), so its fair-use limit never matters.
    /// </summary>
    public class OsrmDriveTimeCalculator : IDriveTimeCalculator
    {
        private const string RouteUrl = "https://router.project-osrm.org/route/v1/driving/";

        private readonly HttpClient httpClient;
        private readonly ILogger<OsrmDriveTimeCalculator> logger;

        public OsrmDriveTimeCalculator(HttpClient httpClient, ILogger<OsrmDriveTimeCalculator> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
        }

        public async Task<int?> DriveMinutesAsync(GeoPoint from, GeoPoint to)
        {
            // OSRM takes longitude first.
            string url = string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1},{2};{3},{4}?overview=false",
                RouteUrl,
                from.Longitude,
                from.Latitude,
                to.Longitude,
                to.Latitude);

            try
            {
                using HttpResponseMessage response = await this.httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                RouteAnswer answer = await response.Content.ReadFromJsonAsync<RouteAnswer>();

                if (answer?.Code != "Ok" || answer.Routes == null || answer.Routes.Count == 0)
                {
                    this.logger.LogWarning("OSRM found no route, answer {Code}", answer?.Code);
                    return null;
                }

                return (int)Math.Round(answer.Routes[0].DurationSeconds / 60.0);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is NotSupportedException)
            {
                this.logger.LogError(ex, "OSRM could not be reached for a drive time");
                return null;
            }
        }

        private sealed class RouteAnswer
        {
            [JsonPropertyName("code")]
            public string Code { get; set; }

            [JsonPropertyName("routes")]
            public List<Route> Routes { get; set; }
        }

        private sealed class Route
        {
            [JsonPropertyName("duration")]
            public double DurationSeconds { get; set; }
        }
    }
}

namespace HandyFix.Services
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Finds a postcode district through postcodes.io: free, no key or account, and built on the
    /// Office for National Statistics' postcode list. Asked once when an admin saves an area, to
    /// place it on the coverage diagram (PROJECT_STATE.md Section 3dm), so its limits never matter.
    /// </summary>
    public class PostcodesIoLocator : IPostcodeLocator
    {
        private const string DistrictUrl = "https://api.postcodes.io/outcodes/";

        private readonly HttpClient httpClient;
        private readonly ILogger<PostcodesIoLocator> logger;

        public PostcodesIoLocator(HttpClient httpClient, ILogger<PostcodesIoLocator> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
        }

        public async Task<GeoPoint?> LocateDistrictAsync(string district)
        {
            if (string.IsNullOrWhiteSpace(district))
            {
                return null;
            }

            try
            {
                using HttpResponseMessage response = await this.httpClient.GetAsync(DistrictUrl + Uri.EscapeDataString(district.Trim()));
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    this.logger.LogWarning("postcodes.io does not know the district {District}", district);
                    return null;
                }

                response.EnsureSuccessStatusCode();
                DistrictAnswer answer = await response.Content.ReadFromJsonAsync<DistrictAnswer>();

                // A few districts (PO boxes, for one) exist but have no place.
                if (answer?.Result?.Latitude is not double latitude || answer.Result.Longitude is not double longitude)
                {
                    this.logger.LogWarning("postcodes.io has no place for the district {District}", district);
                    return null;
                }

                return new GeoPoint(latitude, longitude);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is NotSupportedException)
            {
                this.logger.LogError(ex, "postcodes.io could not be reached for the district {District}", district);
                return null;
            }
        }

        private sealed class DistrictAnswer
        {
            [JsonPropertyName("result")]
            public DistrictPlace Result { get; set; }
        }

        private sealed class DistrictPlace
        {
            [JsonPropertyName("latitude")]
            public double? Latitude { get; set; }

            [JsonPropertyName("longitude")]
            public double? Longitude { get; set; }
        }
    }
}

namespace HandyFix.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Finds a postcode district through postcodes.io: free, no key or account, and built on the
    /// Office for National Statistics' postcode list. Asked when an admin saves an area, to place
    /// it on the coverage diagram (PROJECT_STATE.md Section 3dm) and to find a town's district
    /// from its name (Section 3dn), so its limits never matter.
    /// </summary>
    public class PostcodesIoLocator : IPostcodeLocator
    {
        private const string DistrictUrl = "https://api.postcodes.io/outcodes/";
        private const string PlacesUrl = "https://api.postcodes.io/places?q=";

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

        public async Task<string> FindTownDistrictAsync(string townName, GeoPoint near)
        {
            if (string.IsNullOrWhiteSpace(townName))
            {
                return null;
            }

            string name = townName.Trim();

            try
            {
                // 100 is the most the search gives: "Ham" alone has 32 places, and the one in
                // Richmond is not in the first ten.
                using HttpResponseMessage response = await this.httpClient.GetAsync(PlacesUrl + Uri.EscapeDataString(name) + "&limit=100");
                response.EnsureSuccessStatusCode();
                PlacesAnswer answer = await response.Content.ReadFromJsonAsync<PlacesAnswer>();

                List<Place> places = (answer?.Result ?? new List<Place>())
                    .Where(p => !string.IsNullOrWhiteSpace(p.Outcode) && p.Latitude.HasValue && p.Longitude.HasValue)
                    .ToList();

                // "Molesey" has no place of exactly that name, so West and East Molesey both count.
                List<Place> exact = places.Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
                Place nearest = (exact.Count > 0 ? exact : places)
                    .OrderBy(p => near.DistanceKmTo(new GeoPoint(p.Latitude.Value, p.Longitude.Value)))
                    .FirstOrDefault();

                if (nearest == null)
                {
                    this.logger.LogWarning("postcodes.io knows no place called {TownName}", name);
                    return null;
                }

                return nearest.Outcode.Trim().ToUpperInvariant();
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is NotSupportedException)
            {
                this.logger.LogError(ex, "postcodes.io could not be reached for the town {TownName}", name);
                return null;
            }
        }

        private sealed class PlacesAnswer
        {
            [JsonPropertyName("result")]
            public List<Place> Result { get; set; }
        }

        private sealed class Place
        {
            [JsonPropertyName("name_1")]
            public string Name { get; set; }

            [JsonPropertyName("outcode")]
            public string Outcode { get; set; }

            [JsonPropertyName("latitude")]
            public double? Latitude { get; set; }

            [JsonPropertyName("longitude")]
            public double? Longitude { get; set; }
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

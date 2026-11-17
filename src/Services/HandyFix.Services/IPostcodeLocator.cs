namespace HandyFix.Services
{
    using System.Threading.Tasks;

    public interface IPostcodeLocator
    {
        /// <summary>
        /// Where a postcode district (the first half of a postcode, as "KT22") is: the middle of
        /// it. Null when the district does not exist, has no place, or the lookup did not answer.
        /// Never throws.
        /// </summary>
        Task<GeoPoint?> LocateDistrictAsync(string district);

        /// <summary>
        /// The postcode district of a town found by its name, as "KT8" for "Molesey". Only places
        /// no further than <paramref name="withinKm"/> from <paramref name="near"/> count, so
        /// "Richmond" is the one on the Thames and not a namesake in Yorkshire (PROJECT_STATE.md
        /// Section 3fj). Of those, one called exactly that comes first, then the nearest, so "Ham"
        /// is the one in Richmond, not Hampton Court. Null when no place that near has the name
        /// or the lookup did not answer. Never throws.
        /// </summary>
        Task<string> FindTownDistrictAsync(string townName, GeoPoint near, double withinKm);
    }
}

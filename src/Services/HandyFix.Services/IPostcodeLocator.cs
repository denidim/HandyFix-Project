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
        /// The postcode district of a town found by its name, as "KT8" for "Molesey". Of several
        /// places with the name, one called exactly that comes first, then the one nearest to
        /// <paramref name="near"/>, so "Ashford" is the one in Surrey, not Kent. Null when no
        /// place has the name or the lookup did not answer. Never throws.
        /// </summary>
        Task<string> FindTownDistrictAsync(string townName, GeoPoint near);
    }
}

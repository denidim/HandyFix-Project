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
    }
}

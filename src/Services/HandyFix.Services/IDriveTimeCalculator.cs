namespace HandyFix.Services
{
    using System.Threading.Tasks;

    public interface IDriveTimeCalculator
    {
        /// <summary>
        /// How many minutes the drive between two places takes, by the quickest road route and
        /// without traffic. Null when the route service did not answer. Never throws.
        /// </summary>
        Task<int?> DriveMinutesAsync(GeoPoint from, GeoPoint to);
    }
}

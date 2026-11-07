namespace HandyFix.Services
{
    using System;

    /// <summary>
    /// A place on the map, in degrees: latitude north, longitude east (negative is west).
    /// </summary>
    public readonly struct GeoPoint
    {
        public GeoPoint(double latitude, double longitude)
        {
            this.Latitude = latitude;
            this.Longitude = longitude;
        }

        public double Latitude { get; }

        public double Longitude { get; }

        /// <summary>
        /// The compass direction from this place to another, in degrees from 0 up to (not
        /// including) 360: 0 is north, 90 east, 180 south, 270 west. The usual great-circle
        /// formula; over the few miles the coverage diagram spans it is the direction a map shows.
        /// </summary>
        public double BearingTo(GeoPoint other)
        {
            double fromLatitude = ToRadians(this.Latitude);
            double toLatitude = ToRadians(other.Latitude);
            double longitudeDifference = ToRadians(other.Longitude - this.Longitude);

            double y = Math.Sin(longitudeDifference) * Math.Cos(toLatitude);
            double x = (Math.Cos(fromLatitude) * Math.Sin(toLatitude))
                - (Math.Sin(fromLatitude) * Math.Cos(toLatitude) * Math.Cos(longitudeDifference));

            double degrees = Math.Atan2(y, x) * 180.0 / Math.PI;
            return (degrees + 360.0) % 360.0;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}

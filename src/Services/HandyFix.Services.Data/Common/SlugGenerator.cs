namespace HandyFix.Services.Data.Common
{
    using System.Text.RegularExpressions;

    public static class SlugGenerator
    {
        public static string Slugify(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var slug = name
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("&", "-");

            slug = Regex.Replace(slug, "-{2,}", "-");

            return slug.Trim('-').ToLower();
        }
    }
}

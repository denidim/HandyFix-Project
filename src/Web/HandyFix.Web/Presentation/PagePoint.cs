namespace HandyFix.Web.Presentation
{
    /// <summary>
    /// One point of a service page's list block: its icon, its first sentence (shown in bold in
    /// "What we usually find") and the rest of the line.
    /// </summary>
    public record PagePoint(string Icon, string Lead, string Rest);
}

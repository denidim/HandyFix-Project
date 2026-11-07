namespace HandyFix.Data.Models
{
    /// <summary>
    /// Where a town's name sits beside its dot on the coverage diagram. An area with none set
    /// gets one from its direction: east half to the right, west half to the left. Saved as its
    /// number, so the order here must not change.
    /// </summary>
    public enum DiagramLabelSide
    {
        Right = 1,

        Left = 2,

        Above = 3,

        Below = 4,
    }
}

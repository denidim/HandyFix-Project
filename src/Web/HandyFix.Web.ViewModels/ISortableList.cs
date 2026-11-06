namespace HandyFix.Web.ViewModels
{
    using System;

    /// <summary>
    /// An admin list whose column headings sort it: the jobs, the enquiries and the reviews.
    /// </summary>
    /// <typeparam name="TField">The columns it can be sorted by.</typeparam>
    public interface ISortableList<TField>
        where TField : struct, Enum
    {
        TField SortField { get; }

        bool Descending { get; }
    }
}

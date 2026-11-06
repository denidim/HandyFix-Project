namespace HandyFix.Web.ViewModels
{
    using System;
    using System.Collections.Generic;

    public static class SortableListExtensions
    {
        /// <summary>
        /// Which way a column heading's link sorts: the other way round when the list is sorted
        /// by that column already, ascending when it is not. The jobs, enquiries and reviews pages
        /// each had a copy of this until PROJECT_STATE.md Section 3dj.
        /// </summary>
        public static bool NextDescending<TField>(this ISortableList<TField> list, TField field)
            where TField : struct, Enum
        {
            return EqualityComparer<TField>.Default.Equals(list.SortField, field) ? !list.Descending : false;
        }
    }
}

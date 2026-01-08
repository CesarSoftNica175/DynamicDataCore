namespace DynamicDataCore.Common.Response
{

    /// <summary>
    /// Description: Represents metadata information used for paginated API responses.
    /// <para></para>
    /// This class provides details about the pagination state, such as the current page,
    /// total number of records, and range of items returned in the current request.
    /// It is designed to be included in API responses alongside the main dataset.
    /// <para></para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    public class PaginationMetadata
    {

        /// <summary>
        /// Description: Gets or sets the current page number being displayed.
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Description: Gets or sets the first record index returned in the current page.
        /// </summary>
        public int From { get; set; }

        /// <summary>
        /// Description: Gets or sets the total number of available pages.
        /// </summary>
        public int LastPage { get; set; }

        /// <summary>
        /// Description: Gets or sets the number of items displayed per page.
        /// </summary>
        public int PerPage { get; set; }

        /// <summary>
        /// Description: Gets or sets the last record index returned in the current page.
        /// </summary>
        public int To { get; set; }

        /// <summary>
        /// Description: Gets or sets the total number of items across all pages.
        /// </summary>
        public int Total { get; set; }

    }
}
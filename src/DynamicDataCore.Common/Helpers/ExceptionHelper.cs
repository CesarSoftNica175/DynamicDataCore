using System;

namespace DynamicDataCore.Common.Helpers
{
    // Generate xml documentation for the class
    /// <summary>
    /// Description: Provides helper methods for exception handling and message extraction.
    /// <para></para>
    /// <author>Created By: César Adolfo Solís Alvarez (CSOLIS).</author>
    /// <para></para>
    /// <since>Creation Date: 17/10/2025</since>
    /// </summary>
    public static class ExceptionHelper
    {

        /// <summary>
        /// Extracts a detailed message from the provided exception, including the base exception message and stack trace.
        /// </summary>
        public static string GetDetailedMessage(Exception ex)
        {
            Exception? baseEx = ex.GetBaseException();
            return $"{baseEx.Message} | StackTrace: {baseEx.StackTrace}";
        }

    }
}

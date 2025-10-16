namespace DynamicDataCore.Common.Helpers
{
    public static class ExceptionHelper
    {

        public static string GetDetailedMessage(Exception ex)
        {
            Exception? baseEx = ex.GetBaseException();
            return $"{baseEx.Message} | StackTrace: {baseEx.StackTrace}";
        }

    }
}

internal static class UiText
{
    internal static string T(string japanese, string english) =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? japanese : english;
}

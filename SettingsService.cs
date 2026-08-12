using System;
using System.IO;
using System.Text.Json;

static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new JsonSerializerOptions
        {
            WriteIndented = true
        };

    public static string GetSettingsPath()
    {
        string localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );

        string directory =
            Path.Combine(
                localAppData,
                "ImeLayoutRouter"
            );

        return Path.Combine(
            directory,
            "settings.json"
        );
    }

    public static void Save(
        RoutingConfiguration configuration
    )
    {
        AppSettings settings =
            new AppSettings
            {
                Source =
                    new SourceProfileSettings
                    {
                        LanguageId =
                            configuration.Source.LanguageId,

                        Clsid =
                            configuration.Source.Clsid,

                        ProfileGuid =
                            configuration.Source.ProfileGuid
                    },

                Target =
                    new TargetProfileSettings
                    {
                        LanguageId =
                            configuration.Target.LanguageId,

                        Hkl =
                            configuration.Target.Hkl.ToInt64()
                    }
            };

        string path =
            GetSettingsPath();

        string? directory =
            Path.GetDirectoryName(
                path
            );

        if (directory != null)
        {
            Directory.CreateDirectory(
                directory
            );
        }

        string json =
            JsonSerializer.Serialize(
                settings,
                JsonOptions
            );

        File.WriteAllText(
            path,
            json
        );
    }
}
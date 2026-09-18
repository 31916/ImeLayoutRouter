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
                RouteAllSupportedImes = configuration.RouteAllSupportedImes,
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

        // A crash during saving must not truncate a working configuration.
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, true);
    }

    public static RoutingConfiguration? Load()
    {
        string path =
            GetSettingsPath();

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json =
                File.ReadAllText(
                    path
                );

            AppSettings? settings =
                JsonSerializer.Deserialize<AppSettings>(
                    json
                );

            if (
                settings == null
                ||
                settings.Version is not (1 or 2)
                || settings.Source == null || settings.Target == null
            )
            {
                return null;
            }

            var candidates =
                TsfProfileEnumerator.GetSelectableProfiles();

            InputProfile? source =
                candidates.Sources.Find(
                    profile =>
                        profile.LanguageId
                            == settings.Source.LanguageId
                        &&
                        profile.Clsid
                            == settings.Source.Clsid
                        &&
                        profile.ProfileGuid
                            == settings.Source.ProfileGuid
                );

            InputProfile? target =
                candidates.Targets.Find(
                    profile =>
                        profile.LanguageId
                            == settings.Target.LanguageId
                        &&
                        profile.Hkl.ToInt64()
                            == settings.Target.Hkl
                );

            if (source == null && settings.RouteAllSupportedImes)
                source = candidates.Sources.FirstOrDefault();

            if (
                source == null
                ||
                target == null
            )
            {
                return null;
            }

            return new RoutingConfiguration(
                source,
                target,
                settings.RouteAllSupportedImes ? candidates.Sources : null
            );
        }
        catch (
            JsonException
        )
        {
            return null;
        }
        catch (
            IOException
        )
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

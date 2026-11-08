using System.IO;

namespace VRCLearn.App.Services;

public static class LicenseText
{
    public static string Read() =>
        $"{ReadResource("ProjectLicense").TrimEnd()}{Environment.NewLine}{Environment.NewLine}{ReadResource("ThirdPartyNotices")}";

    private static string ReadResource(string resourceName)
    {
        using var stream = typeof(LicenseText).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"License resource {resourceName} is missing.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}

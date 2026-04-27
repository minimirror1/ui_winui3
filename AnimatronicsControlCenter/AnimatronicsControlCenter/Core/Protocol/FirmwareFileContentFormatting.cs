namespace AnimatronicsControlCenter.Core.Protocol;

public static class FirmwareFileContentFormatting
{
    public static string NormalizeLineEndingsForDevice(string content)
        => content.Replace("\r\n", "\n")
                  .Replace("\r", "\n")
                  .Replace("\n", "\r\n");
}

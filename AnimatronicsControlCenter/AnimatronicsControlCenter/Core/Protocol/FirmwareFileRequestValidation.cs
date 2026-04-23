using System.Text;

namespace AnimatronicsControlCenter.Core.Protocol;

public static class FirmwareFileRequestValidation
{
    public readonly record struct ValidationResult(bool IsValid, string ErrorMessage);

    public static ValidationResult Validate(string path, string content)
    {
        int pathBytes = Encoding.UTF8.GetByteCount(path ?? string.Empty);
        if (pathBytes >= BinaryProtocolConst.AppPathMaxLen)
        {
            return new ValidationResult(
                IsValid: false,
                ErrorMessage: $"file path exceeds firmware limit ({BinaryProtocolConst.MaxPathUtf8Bytes} UTF-8 bytes).");
        }

        int contentBytes = Encoding.UTF8.GetByteCount(content ?? string.Empty);
        if (contentBytes >= BinaryProtocolConst.AppContentMaxLen)
        {
            return new ValidationResult(
                IsValid: false,
                ErrorMessage: $"file content exceeds firmware limit ({BinaryProtocolConst.MaxContentUtf8Bytes} UTF-8 bytes).");
        }

        return new ValidationResult(IsValid: true, ErrorMessage: string.Empty);
    }
}

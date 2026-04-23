using System;

namespace AnimatronicsControlCenter.Core.Protocol;

public static class SaveFileResponseProjection
{
    public readonly record struct SaveFileResponseResult(
        bool Success,
        string StatusMessage,
        string ErrorDetail,
        string ConfirmedPath);

    public static SaveFileResponseResult Evaluate(byte[]? responseBytes, string expectedPath)
    {
        if (responseBytes == null)
            return Failure("No response from device.");

        if (!BinaryDeserializer.TryParseResponseHeader(responseBytes, out var header))
            return Failure("Invalid response header.");

        int payloadStart = BinaryProtocolConst.ResponseHeaderSize;
        if (responseBytes.Length < payloadStart + header.PayloadLen)
            return Failure("Incomplete response payload.");

        ReadOnlySpan<byte> payload = responseBytes.AsSpan(payloadStart, header.PayloadLen);
        if (!BinaryDeserializer.IsOk(header))
        {
            string error = $"Device returned {header.Status} for {header.Cmd}.";
            if (!payload.IsEmpty)
            {
                var (code, message) = BinaryDeserializer.ParseErrorResponse(payload);
                error = string.IsNullOrWhiteSpace(message)
                    ? BinaryProtocolErrorText.Describe(code, header.Cmd)
                    : message;
            }

            return Failure(error);
        }

        string confirmedPath = BinaryDeserializer.ParseSaveFileResponse(payload);
        if (!string.Equals(confirmedPath, expectedPath, StringComparison.Ordinal))
            return Failure("invalid device response.");

        return new SaveFileResponseResult(
            Success: true,
            StatusMessage: $"Saved file: {confirmedPath}",
            ErrorDetail: string.Empty,
            ConfirmedPath: confirmedPath);
    }

    private static SaveFileResponseResult Failure(string detail)
        => new(
            Success: false,
            StatusMessage: $"Failed to save file: {detail}",
            ErrorDetail: detail,
            ConfirmedPath: string.Empty);
}

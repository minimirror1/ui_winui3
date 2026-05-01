using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Infrastructure;

internal static class BackendHttpRequest
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryCreateUri(ISettingsService settings, string path, out Uri uri, out string message)
    {
        uri = null!;
        message = string.Empty;

        string baseUrl = settings.BackendBaseUrl.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            message = "Backend base URL is invalid.";
            return false;
        }

        string combined = $"{baseUri.ToString().TrimEnd('/')}/{path.TrimStart('/')}";
        if (!Uri.TryCreate(combined, UriKind.Absolute, out uri!))
        {
            message = "Backend request URL is invalid.";
            return false;
        }

        return true;
    }

    public static HttpRequestMessage Create(ISettingsService settings, HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        string token = settings.BackendBearerToken.Trim();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    public static StringContent JsonContent<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");
    }
}

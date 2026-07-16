using AnimatronicsControlCenter.Core.Interfaces;
#if WINDOWS
using Windows.Security.Credentials;
#endif

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendApiKeyStore : IBackendApiKeyStore
{
#if WINDOWS
    private const string ResourceName = "AnimatronicsControlCenter.BackendApi";
    private const string UserName = "X-API-Key";

    public string Load()
    {
        try
        {
            foreach (PasswordCredential credential in new PasswordVault().RetrieveAll())
            {
                if (credential.Resource == ResourceName && credential.UserName == UserName)
                {
                    credential.RetrievePassword();
                    return credential.Password ?? string.Empty;
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    public void Save(string apiKey)
    {
        try
        {
            var vault = new PasswordVault();
            RemoveExistingCredential(vault);

            string normalizedApiKey = apiKey.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedApiKey))
            {
                vault.Add(new PasswordCredential(ResourceName, UserName, normalizedApiKey));
            }
        }
        catch
        {
        }
    }

    private static void RemoveExistingCredential(PasswordVault vault)
    {
        try
        {
            foreach (PasswordCredential credential in vault.RetrieveAll())
            {
                if (credential.Resource == ResourceName && credential.UserName == UserName)
                {
                    vault.Remove(credential);
                }
            }
        }
        catch
        {
        }
    }
#else
    private string _apiKey = string.Empty;

    public string Load() => _apiKey;

    public void Save(string apiKey) => _apiKey = apiKey.Trim();
#endif
}

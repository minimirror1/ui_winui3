namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendApiKeyStore
{
    string Load();
    void Save(string apiKey);
}

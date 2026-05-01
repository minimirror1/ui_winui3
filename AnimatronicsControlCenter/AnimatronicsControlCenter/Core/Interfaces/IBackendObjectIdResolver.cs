namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendObjectIdResolver
{
    string? ResolveObjectId(int deviceId);
}

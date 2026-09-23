namespace AnimatronicsControlCenter.Core.Utilities;

/// <summary>
/// 화면에 보여 줄 앱 버전 문자열.
///
/// 버전 숫자의 출처는 Package.appxmanifest 하나뿐이다. 실행할 때 패키지 정보에서 읽으므로
/// 코드에 버전을 따로 적어 두지 않는다. 릴리스 때 manifest 만 올리면 화면 표기도 따라온다.
/// </summary>
public static class AppVersionText
{
    /// 패키지 정보를 읽지 못했을 때 쓰는 문구. 패키지로 실행하지 않은 빌드가 여기 해당한다.
    public const string Unavailable = "개발 빌드";

    /// <summary>
    /// 릴리스 태그(v1.1.17.0), 커밋 제목(SW v1.1.17.0)과 같은 표기를 쓴다.
    /// 네 자리를 모두 적어야 사용자가 말한 버전을 저장소에서 그대로 찾을 수 있다.
    /// </summary>
    public static string Format(int major, int minor, int build, int revision)
        => $"v{major}.{minor}.{build}.{revision}";
}

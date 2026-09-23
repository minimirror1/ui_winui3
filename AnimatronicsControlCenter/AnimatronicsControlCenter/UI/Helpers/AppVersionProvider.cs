using System;
using AnimatronicsControlCenter.Core.Utilities;

namespace AnimatronicsControlCenter.UI.Helpers
{
    /// <summary>
    /// 실행 중인 앱의 버전. Package.appxmanifest 가 유일한 출처이며 실행할 때 패키지 정보에서 읽는다.
    /// launchSettings 의 프로필이 MsixPackage 하나뿐이라 개발 실행에서도 값이 들어온다.
    /// </summary>
    public static class AppVersionProvider
    {
        /// 네 자리 버전 (예: 1.1.17.0). 패키지 정보를 못 읽으면 null.
        public static string? Version { get; } = ReadVersion();

        /// 화면 표기 (예: v1.1.17.0). 못 읽으면 "개발 빌드".
        public static string Display { get; } =
            Version is null ? AppVersionText.Unavailable : $"v{Version}";

        private static string? ReadVersion()
        {
            try
            {
                var v = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
            catch (InvalidOperationException)
            {
                // 패키지로 실행하지 않으면 Package.Current 가 던진다.
                return null;
            }
        }
    }
}

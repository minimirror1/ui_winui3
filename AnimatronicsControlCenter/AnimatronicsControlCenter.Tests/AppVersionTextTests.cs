using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class AppVersionTextTests
{
    [TestMethod]
    public void Format_MatchesReleaseTagSpelling()
    {
        // 화면 표기는 릴리스 태그(v1.1.17.0)·커밋 제목(SW v1.1.17.0)과 같아야
        // 사용자가 말한 버전을 저장소에서 바로 찾을 수 있다.
        Assert.AreEqual("v1.1.17.0", AppVersionText.Format(1, 1, 17, 0));
    }

    [TestMethod]
    public void Format_KeepsAllFourParts()
    {
        // 네 번째 자리가 0이라고 생략하면 태그와 글자가 달라진다.
        Assert.AreEqual("v2.0.0.0", AppVersionText.Format(2, 0, 0, 0));
        Assert.AreEqual("v1.1.17.3", AppVersionText.Format(1, 1, 17, 3));
    }

    [TestMethod]
    public void Unavailable_IsNotEmpty()
    {
        // 패키지 정보를 못 읽어도 자리가 비어 보이면 안 된다.
        Assert.AreNotEqual(string.Empty, AppVersionText.Unavailable);
    }
}

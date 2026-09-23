using System.Text.RegularExpressions;
using System.Xml.Linq;
using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class ReleaseNotesCatalogTests
{
    [TestMethod]
    public void Latest_MatchesManifestVersion()
    {
        // 새 버전을 내면서 릴리즈 노트를 빠뜨리면 여기서 걸린다.
        string manifest = ProjectPath("AnimatronicsControlCenter", "Package.appxmanifest");
        var identity = XDocument.Load(manifest).Descendants()
            .First(e => e.Name.LocalName == "Identity");
        string manifestVersion = identity.Attribute("Version")!.Value;

        Assert.AreEqual(manifestVersion, ReleaseNotesCatalog.Latest.Version,
            "Package.appxmanifest 버전에 해당하는 릴리즈 노트가 카탈로그 맨 앞에 있어야 합니다.");
    }

    [TestMethod]
    public void All_IsOrderedNewestFirst()
    {
        var versions = ReleaseNotesCatalog.All.Select(n => ParseVersion(n.Version)).ToList();

        for (int i = 1; i < versions.Count; i++)
        {
            Assert.IsTrue(versions[i - 1] > versions[i],
                $"{ReleaseNotesCatalog.All[i - 1].Version} 가 {ReleaseNotesCatalog.All[i].Version} 보다 뒤에 와야 합니다.");
        }
    }

    [TestMethod]
    public void All_HasNoDuplicateVersions()
    {
        var versions = ReleaseNotesCatalog.All.Select(n => n.Version).ToList();

        CollectionAssert.AllItemsAreUnique(versions);
    }

    [TestMethod]
    public void All_UsesFourPartVersionsAndIsoDates()
    {
        foreach (var note in ReleaseNotesCatalog.All)
        {
            // 태그(v1.1.17.0)·커밋 제목과 같은 표기여야 저장소에서 바로 찾을 수 있다.
            StringAssert.Matches(note.Version, new Regex(@"^\d+\.\d+\.\d+\.\d+$"),
                $"{note.Version} 은 네 자리 버전이 아닙니다.");
            StringAssert.Matches(note.Date, new Regex(@"^\d{4}-\d{2}-\d{2}$"),
                $"{note.Version} 의 날짜 형식이 올바르지 않습니다.");
            Assert.AreEqual($"v{note.Version}", note.DisplayVersion);
        }
    }

    [TestMethod]
    public void All_EveryNoteHasHeadlineAndItems()
    {
        foreach (var note in ReleaseNotesCatalog.All)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(note.Headline), $"{note.Version}: 요약이 비었습니다.");
            Assert.IsTrue(note.Items.Count > 0, $"{note.Version}: 항목이 없습니다.");

            foreach (var item in note.Items)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Glyph), $"{note.Version}: 아이콘이 비었습니다.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Title), $"{note.Version}: 항목 제목이 비었습니다.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.Body), $"{note.Version}: 항목 설명이 비었습니다.");
            }
        }
    }

    [TestMethod]
    public void All_CoversEveryReleasedVersionInHistory()
    {
        // 저장소 이력에 있는 SW 버전 커밋은 모두 노트를 가져야 한다.
        string[] released =
        [
            "1.1.7.0", "1.1.7.1", "1.1.7.2", "1.1.8.0", "1.1.9.0", "1.1.9.1", "1.1.9.2",
            "1.1.10.0", "1.1.11.0", "1.1.14.0", "1.1.15.0", "1.1.16.0", "1.1.17.0",
        ];

        foreach (string version in released)
        {
            Assert.IsNotNull(ReleaseNotesCatalog.Find(version), $"{version} 릴리즈 노트가 없습니다.");
        }
    }

    [TestMethod]
    public void Find_ReturnsNullForUnknownVersion()
    {
        // 1.1.12.0 과 1.1.13.0 은 실제로 배포된 적이 없다.
        Assert.IsNull(ReleaseNotesCatalog.Find("1.1.12.0"));
        Assert.IsNull(ReleaseNotesCatalog.Find(null));
    }

    private static Version ParseVersion(string raw) => new(raw);

    private static string ProjectPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(segments)}");
    }
}

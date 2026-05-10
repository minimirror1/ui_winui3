using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendSettingsPageXamlTests
{
    [TestMethod]
    public void BackendSettingsPage_ContainsServerAndLocalSettingsControls()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "BackendSettingsPage.xaml"));

        foreach (string expected in new[]
        {
            "ServerValuesPanel",
            "LocalSettingsPanel",
            "Padding=\"0,0,16,0\"",
            "AvailableCountryCodes",
            "SelectedCountryCode",
            "ServerStoreList",
            "SelectedServerStore",
            "ServerPcList",
            "SelectedServerPc",
            "ServerObjectListHeader",
            "서버 오브제",
            "IsRegistrationAvailable",
            "ShouldShowRegistrationCountryCodeHint",
            "Country Code를 먼저 선택하면 데이터 관리가 활성화됩니다.",
            "ApplyServerValuesCommand",
            "CompareWithServerCommand",
            "SaveCommand",
            "BackendBaseUrl",
            "IsServerConnectionEditing",
            "ServerConnectionEditButtonText",
            "ToggleServerConnectionEditingCommand",
            "서버 접속 정보는 잠겨 있습니다.",
            "BackendStoreId",
            "BackendPcId",
            "BackendPcName",
            "BackendSoftwareVersion",
            "BackendDeviceObjectMappingsText",
            "MappedServerObjects",
            "오브제 이름",
            "서버 오브제 ID",
            "WinUI 로컬 ID",
            "EditObjectMappingsButton",
            "OnEditObjectMappingsClicked",
            "오브제 매핑 수정",
            "OpenLocalSettingsFileButton",
            "OnOpenLocalSettingsFileClicked",
            "로컬 설정 파일 열기",
            "서버 데이터(Store/PC/Object) 등록/수정",
        })
        {
            StringAssert.Contains(xaml, expected);
        }
    }

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

        Assert.Fail($"Could not find project file: {Path.Combine(segments)}");
        return string.Empty;
    }
}

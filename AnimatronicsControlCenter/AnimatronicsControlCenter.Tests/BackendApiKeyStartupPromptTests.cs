using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendApiKeyStartupPromptTests
{
    [TestMethod]
    public void PromptViewModel_RequiresNonWhitespaceKeyAndReturnsTrimmedValue()
    {
        var viewModel = new BackendApiKeyPromptViewModel();

        Assert.IsFalse(viewModel.CanSave);
        viewModel.ApiKey = "   ";
        Assert.IsFalse(viewModel.CanSave);

        viewModel.ApiKey = "  api-key-1  ";

        Assert.IsTrue(viewModel.CanSave);
        Assert.AreEqual("api-key-1", viewModel.ApiKeyToSave);
    }

    [TestMethod]
    public void PromptDialog_ProvidesSecureSaveAndDeferActions()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "BackendApiKeyPromptDialog.xaml"));

        StringAssert.Contains(xaml, "PrimaryButtonText=\"저장\"");
        StringAssert.Contains(xaml, "CloseButtonText=\"나중에\"");
        StringAssert.Contains(xaml, "IsPrimaryButtonEnabled=\"{x:Bind ViewModel.CanSave, Mode=OneWay}\"");
        StringAssert.Contains(xaml, "PasswordRevealMode=\"Peek\"");
        StringAssert.Contains(xaml, "X-API-Key");
        StringAssert.Contains(xaml, "다음 실행 때 다시 안내합니다.");
    }

    [TestMethod]
    public void App_PromptsBeforeStartingBackendServicesWhenApiKeyIsMissing()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "App.xaml.cs"));

        StringAssert.Contains(code, "protected override async void OnLaunched");
        StringAssert.Contains(code, "string.IsNullOrWhiteSpace(settingsService.BackendApiKey)");
        StringAssert.Contains(code, "new BackendApiKeyPromptDialog()");
        StringAssert.Contains(code, "ContentDialogResult.Primary");
        StringAssert.Contains(code, "settingsService.BackendApiKey = dialog.ApiKey");
        StringAssert.Contains(code, "settingsService.Save()");
        StringAssert.Contains(code, "StartBackendServices()");
        StringAssert.Contains(code, "WaitForXamlRootAsync(root)");
        StringAssert.Contains(code, "root.Loaded += loadedHandler");
        StringAssert.Contains(code, "dialog.XamlRoot = await xamlRootTask");

        int waitSetupIndex = code.IndexOf("WaitForXamlRootAsync(root)", StringComparison.Ordinal);
        int activateIndex = code.IndexOf("m_window.Activate()", StringComparison.Ordinal);
        int showIndex = code.IndexOf("dialog.ShowAsync()", StringComparison.Ordinal);
        int startIndex = code.IndexOf("StartBackendServices()", StringComparison.Ordinal);
        Assert.IsTrue(waitSetupIndex >= 0 && waitSetupIndex < activateIndex);
        Assert.IsTrue(activateIndex < showIndex && showIndex < startIndex);
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

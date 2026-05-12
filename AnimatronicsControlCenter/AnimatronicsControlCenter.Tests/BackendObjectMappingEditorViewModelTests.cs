using System.Collections.Generic;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendObjectMappingEditorViewModelTests
{
    [TestMethod]
    public void Constructor_ShowsServerObjectsWithExistingLocalIds()
    {
        var viewModel = new BackendObjectMappingEditorViewModel(
            new[]
            {
                new BackendServerObjectMappingSource("obj-1", "Robot A"),
                new BackendServerObjectMappingSource("obj-2", "Robot B"),
            },
            new Dictionary<int, string> { [2] = "obj-1" });

        Assert.AreEqual(2, viewModel.ServerObjectMappings.Count);
        Assert.AreEqual("obj-1", viewModel.ServerObjectMappings[0].ObjectId);
        Assert.AreEqual("Robot A", viewModel.ServerObjectMappings[0].ObjectName);
        Assert.AreEqual("2", viewModel.ServerObjectMappings[0].LocalObjectIdText);
        Assert.AreEqual(string.Empty, viewModel.ServerObjectMappings[1].LocalObjectIdText);
    }

    [TestMethod]
    public void Constructor_ShowsMappingsMissingFromCurrentServerListSeparately()
    {
        var viewModel = new BackendObjectMappingEditorViewModel(
            new[] { new BackendServerObjectMappingSource("obj-1", "Robot A") },
            new Dictionary<int, string>
            {
                [2] = "obj-1",
                [3] = "old-obj",
            });

        Assert.AreEqual(1, viewModel.MissingServerObjectMappings.Count);
        Assert.AreEqual("old-obj", viewModel.MissingServerObjectMappings[0].ObjectId);
        Assert.AreEqual("3", viewModel.MissingServerObjectMappings[0].LocalObjectIdText);
    }

    [TestMethod]
    public void TryBuildMappings_AllowsEmptyValuesAndDeletesMapping()
    {
        var viewModel = new BackendObjectMappingEditorViewModel(
            new[] { new BackendServerObjectMappingSource("obj-1", "Robot A") },
            new Dictionary<int, string> { [2] = "obj-1" });
        viewModel.ServerObjectMappings[0].LocalObjectIdText = string.Empty;

        bool ok = viewModel.TryBuildMappings(out Dictionary<int, string> mappings);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, mappings.Count);
    }

    [TestMethod]
    public void TryBuildMappings_RejectsDuplicateLocalObjectIds()
    {
        var viewModel = new BackendObjectMappingEditorViewModel(
            new[]
            {
                new BackendServerObjectMappingSource("obj-1", "Robot A"),
                new BackendServerObjectMappingSource("obj-2", "Robot B"),
            },
            new Dictionary<int, string>());
        viewModel.ServerObjectMappings[0].LocalObjectIdText = "2";
        viewModel.ServerObjectMappings[1].LocalObjectIdText = "2";

        bool ok = viewModel.TryBuildMappings(out Dictionary<int, string> mappings);

        Assert.IsFalse(ok);
        Assert.AreEqual(0, mappings.Count);
        StringAssert.Contains(viewModel.ValidationMessage, "중복");
    }
}

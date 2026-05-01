using System.Collections.Generic;
using System.Linq;
using AnimatronicsControlCenter.Core.Backend;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendSettingsComparisonTests
{
    [TestMethod]
    public void Compare_ReturnsFieldLevelMismatchReasons()
    {
        var server = new BackendServerSnapshot(
            StoreId: "store-1",
            StoreName: "Seoul Store",
            StoreCountryCode: "KR",
            PcId: "pc-1",
            PcName: "Main PC",
            SwVersion: "1.1.1.0",
            Objects: new[] { new BackendServerObjectSnapshot("obj-1", "Robot A") });

        var local = new BackendLocalSettingsSnapshot(
            StoreId: "store-1",
            StoreName: "Seoul Store",
            StoreCountryCode: "JP",
            PcId: "pc-2",
            PcName: "pc_name_001",
            SwVersion: "1.1.2.0",
            DeviceObjectMappings: new Dictionary<int, string> { [2] = "obj-missing" });

        var result = BackendSettingsComparison.Compare(server, local);

        Assert.IsTrue(result.CanCompare);
        Assert.IsTrue(result.Fields.Single(x => x.FieldName == "StoreId").IsMatch);
        Assert.IsFalse(result.Fields.Single(x => x.FieldName == "StoreCountryCode").IsMatch);
        StringAssert.Contains(result.Fields.Single(x => x.FieldName == "StoreCountryCode").Message, "서버 값");
        StringAssert.Contains(result.Fields.Single(x => x.FieldName == "SwVersion").Message, "sw_version");
        StringAssert.Contains(result.Fields.Single(x => x.FieldName == "PcId").Message, "PC ID");
        StringAssert.Contains(result.Fields.Single(x => x.FieldName == "DeviceObjectMappings").Message, "Object ID");
    }

    [TestMethod]
    public void Compare_NullServer_ReturnsCannotCompare()
    {
        var local = new BackendLocalSettingsSnapshot(
            StoreId: "store-1",
            StoreName: "Seoul Store",
            StoreCountryCode: "KR",
            PcId: "pc-1",
            PcName: "Main PC",
            SwVersion: "1.1.1.0",
            DeviceObjectMappings: new Dictionary<int, string>());

        var result = BackendSettingsComparison.Compare(null, local);

        Assert.IsFalse(result.CanCompare);
        StringAssert.Contains(result.SummaryMessage, "서버 값을 먼저 조회");
    }
}

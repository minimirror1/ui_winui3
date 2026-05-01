using System;
using System.Text.Json;
using AnimatronicsControlCenter.Core.Backend;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendDtoSerializationTests
{
    [TestMethod]
    public void ObjectLogRequest_UsesBackendJsonFieldNames()
    {
        var request = new BackendObjectLogRequest(
            PowerStatus: "ON",
            OperationStatus: "PLAY",
            PowerConsumption: null,
            ErrorData: Array.Empty<BackendErrorData>());

        string json = JsonSerializer.Serialize(request);

        StringAssert.Contains(json, "\"power_status\":\"ON\"");
        StringAssert.Contains(json, "\"operation_status\":\"PLAY\"");
        StringAssert.Contains(json, "\"power_consumption\":null");
        StringAssert.Contains(json, "\"error_data\":[]");
    }

    [TestMethod]
    public void PcUpdateRequest_UsesBackendJsonFieldNames()
    {
        string json = JsonSerializer.Serialize(new BackendPcUpdateRequest("pc_name_001", "1.1.1.0"));

        StringAssert.Contains(json, "\"pc_name\":\"pc_name_001\"");
        StringAssert.Contains(json, "\"sw_version\":\"1.1.1.0\"");
    }

    [TestMethod]
    public void StoreDetailResponse_ReadsBackendJsonFieldNames()
    {
        const string json = """
        {
          "store_id": "store-1",
          "store_name": "Seoul Store",
          "country_code": "KR",
          "pcs": [
            {
              "pc_id": "pc-1",
              "pc_name": "Main PC",
              "sw_version": "1.1.1.0",
              "objects": [
                { "id": "obj-1", "object_name": "Robot A", "power_status": "ON", "error_data": [] }
              ]
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<BackendStoreDetailResponse>(json);

        Assert.IsNotNull(response);
        Assert.AreEqual("store-1", response.StoreId);
        Assert.AreEqual("KR", response.CountryCode);
        Assert.AreEqual("pc-1", response.Pcs[0].PcId);
        Assert.AreEqual("1.1.1.0", response.Pcs[0].SwVersion);
        Assert.AreEqual("obj-1", response.Pcs[0].Objects[0].Id);
    }
}

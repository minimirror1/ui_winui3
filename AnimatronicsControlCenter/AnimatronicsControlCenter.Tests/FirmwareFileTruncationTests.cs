using System.Text;
using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class FirmwareFileTruncationTests
{
    // ── 절단 판정 ────────────────────────────────────────────────

    [TestMethod]
    public void Inspect_WhenReceivedBytesFallShortOfDeviceSize_ReportsTruncation()
    {
        var result = FirmwareFileTruncationCheck.Inspect(deviceFileSize: 1024, receivedBytes: 511);

        Assert.IsTrue(result.IsTruncated);
        Assert.AreEqual(1024, result.DeviceFileSize);
        Assert.AreEqual(511, result.ReceivedBytes);
        Assert.AreNotEqual(string.Empty, result.WarningMessage);
    }

    [TestMethod]
    public void Inspect_WhenReceivedBytesMatchDeviceSize_ReportsIntact()
    {
        var result = FirmwareFileTruncationCheck.Inspect(deviceFileSize: 511, receivedBytes: 511);

        Assert.IsFalse(result.IsTruncated);
        Assert.AreEqual(string.Empty, result.WarningMessage);
    }

    [DataTestMethod]
    [DataRow(0L, 0)]
    [DataRow(0L, 120)]
    [DataRow(-1L, 120)]
    public void Inspect_WhenDeviceSizeIsUnknown_ReportsIntact(long deviceFileSize, int receivedBytes)
    {
        // size==0 은 "빈 파일"과 "크기 미보고"를 구분할 수 없으므로 판정하지 않는다.
        var result = FirmwareFileTruncationCheck.Inspect(deviceFileSize, receivedBytes);

        Assert.IsFalse(result.IsTruncated);
    }

    [TestMethod]
    public void Inspect_WhenReceivedExceedsDeviceSize_ReportsIntact()
    {
        var result = FirmwareFileTruncationCheck.Inspect(deviceFileSize: 100, receivedBytes: 120);

        Assert.IsFalse(result.IsTruncated);
    }

    [TestMethod]
    public void Inspect_WarningStatesBothSizesAndThatSavingIsImpossible()
    {
        var result = FirmwareFileTruncationCheck.Inspect(deviceFileSize: 1024, receivedBytes: 511);

        StringAssert.Contains(result.WarningMessage, "1024");
        StringAssert.Contains(result.WarningMessage, "511");
        // 펌웨어 한계(511바이트)를 넘는 파일은 이 앱에서 저장 자체가 불가능하다는 점이 드러나야 한다.
        StringAssert.Contains(result.WarningMessage, BinaryProtocolConst.MaxContentUtf8Bytes.ToString());
    }

    // ── 와이어 content 길이 파싱 ─────────────────────────────────

    [TestMethod]
    public void ParseGetFileResponse_ReportsWireContentLength()
    {
        byte[] content = Encoding.UTF8.GetBytes("AC2,1.1,CW,8000\r\n");
        byte[] payload = BuildGetFilePayload("Setting/MT_ST.TXT", content);

        var (path, text) = BinaryDeserializer.ParseGetFileResponse(payload, out int contentByteLength);

        Assert.AreEqual("Setting/MT_ST.TXT", path);
        Assert.AreEqual("AC2,1.1,CW,8000\r\n", text);
        Assert.AreEqual(content.Length, contentByteLength);
    }

    [TestMethod]
    public void ParseGetFileResponse_ReportsWireLength_WhenTruncationSplitsMultiByteCharacter()
    {
        // 장치가 3바이트 한글 문자 중간에서 잘라 보낸 경우.
        byte[] full = Encoding.UTF8.GetBytes("가나다");       // 9 bytes
        byte[] cut = full[..7];                                // 3번째 글자가 1바이트만 도착
        byte[] payload = BuildGetFilePayload("Setting/MT_ST.TXT", cut);

        var (_, text) = BinaryDeserializer.ParseGetFileResponse(payload, out int contentByteLength);

        Assert.AreEqual(7, contentByteLength, "와이어에 실린 바이트 수를 그대로 보고해야 한다.");
        Assert.AreNotEqual(7, Encoding.UTF8.GetByteCount(text),
            "재인코딩 길이는 U+FFFD 때문에 커진다 — 절단 판정에 쓰면 안 된다.");
    }

    [TestMethod]
    public void TruncationCheck_WithWireLength_DetectsMultiByteSplitThatReencodingWouldMiss()
    {
        byte[] full = Encoding.UTF8.GetBytes("가나다");       // 장치 파일 크기 9바이트
        byte[] cut = full[..7];
        byte[] payload = BuildGetFilePayload("Setting/MT_ST.TXT", cut);

        var (_, text) = BinaryDeserializer.ParseGetFileResponse(payload, out int contentByteLength);

        Assert.IsTrue(FirmwareFileTruncationCheck.Inspect(full.Length, contentByteLength).IsTruncated,
            "와이어 길이로는 절단을 잡아야 한다.");
        Assert.IsFalse(FirmwareFileTruncationCheck.Inspect(full.Length, Encoding.UTF8.GetByteCount(text)).IsTruncated,
            "재인코딩 길이로는 절단을 놓친다 — 이 회귀를 막기 위한 대조군.");
    }

    [TestMethod]
    public void ParseGetFileResponse_ReportsZeroLength_WhenPayloadIsIncomplete()
    {
        byte[] content = Encoding.UTF8.GetBytes("ABCDEF");
        byte[] payload = BuildGetFilePayload("Setting/MT_ST.TXT", content);
        byte[] cropped = payload[..^3];   // 선언된 content_len 보다 실제 바이트가 모자람

        BinaryDeserializer.ParseGetFileResponse(cropped, out int contentByteLength);

        Assert.AreEqual(0, contentByteLength);
    }

    [TestMethod]
    public void ParseGetFileResponse_MatchesRealDeviceFrameCappedAtFirmwareLimit()
    {
        // 실제 장치가 Setting/MT_ST.TXT 를 511바이트로 잘라 보낸 응답과 같은 형태.
        byte[] content = Encoding.UTF8.GetBytes(new string('A', BinaryProtocolConst.MaxContentUtf8Bytes));
        byte[] payload = BuildGetFilePayload("Setting/MT_ST.TXT", content);

        BinaryDeserializer.ParseGetFileResponse(payload, out int contentByteLength);

        Assert.AreEqual(511, contentByteLength);
        Assert.IsTrue(FirmwareFileTruncationCheck.Inspect(deviceFileSize: 1120, contentByteLength).IsTruncated);
    }

    // ── ViewModel / XAML 배선 ────────────────────────────────────

    [TestMethod]
    public void DeviceDetailViewModel_GatesSaveCommandOnTruncationFlag()
    {
        string source = ViewModelSource();

        StringAssert.Contains(source, "[RelayCommand(CanExecute = nameof(CanSaveFile))]");
        StringAssert.Contains(source, "private bool CanSaveFile() => !IsFileSaveBlocked;");
        StringAssert.Contains(source, "SaveFileCommand.NotifyCanExecuteChanged();");
    }

    [TestMethod]
    public void DeviceDetailViewModel_UsesWireContentLengthForTruncationCheck()
    {
        string source = ViewModelSource();

        StringAssert.Contains(source, "ParseGetFileResponse(payload, out int contentByteLength)");
        StringAssert.Contains(source, "FirmwareFileTruncationCheck.Inspect(file.Size, receivedBytes)");
        Assert.IsFalse(source.Contains("Encoding.UTF8.GetByteCount(content)"),
            "재인코딩 길이로 판정하면 멀티바이트 절단을 놓친다.");
    }

    [TestMethod]
    public void DeviceDetailViewModel_IgnoresFileResponseWhenSelectionChanged()
    {
        string source = ViewModelSource();

        StringAssert.Contains(source, "if (!ReferenceEquals(SelectedFile, file)) return;");
    }

    [TestMethod]
    public void DeviceDetailViewModel_BlocksSaveWhenFileContentCannotBeRead()
    {
        string source = ViewModelSource();

        int failureIndex = source.IndexOf("Failed to load file content:", StringComparison.Ordinal);
        Assert.IsTrue(failureIndex >= 0);

        string beforeFailure = source[..failureIndex];
        StringAssert.Contains(beforeFailure[^400..], "BlockFileSave(",
            "읽기 실패 시에도 저장을 막아야 이전 파일의 허용 상태가 새지 않는다.");
    }

    [TestMethod]
    public void DeviceDetailViewModel_ClearsSaveBlockWhereverFileContentIsReset()
    {
        string[] lines = ViewModelSource().Split((char)10);

        int resets = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("FileContent = string.Empty;")) continue;
            resets++;
            Assert.IsTrue(i + 1 < lines.Length && lines[i + 1].Contains("ClearFileSaveBlock();"),
                $"{i + 1}번째 줄의 FileContent 초기화 직후 ClearFileSaveBlock() 이 없습니다. " +
                "저장 차단 플래그가 남으면 이전 파일 상태가 새어 나갑니다.");
        }

        Assert.IsTrue(resets >= 4, $"FileContent 초기화 지점이 {resets}곳만 발견됐습니다.");
    }

    [TestMethod]
    public void DeviceDetailViewModel_RunsTruncationCheckWhereverContentIsAssigned()
    {
        // 데이터가 실제로 사라지는 방향: 내용을 채우면서 절단 검사를 빠뜨리면 저장이 열린 채 남는다.
        string[] lines = ViewModelSource().Split((char)10);

        int assignments = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("FileContent = content;")) continue;
            assignments++;
            Assert.IsTrue(i + 1 < lines.Length && lines[i + 1].Contains("ApplyTruncationCheck("),
                $"{i + 1}번째 줄에서 파일 내용을 채운 직후 ApplyTruncationCheck() 가 없습니다. " +
                "검사를 거치지 않은 내용으로 저장이 허용됩니다.");
        }

        Assert.AreEqual(1, assignments, "파일 내용을 채우는 경로는 한 곳이어야 한다.");
    }

    [TestMethod]
    public void DeviceDetailPage_ShowsSaveBlockedWarningBar()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DeviceDetailPage.xaml"));

        StringAssert.Contains(xaml, "IsOpen=\"{x:Bind ViewModel.IsFileSaveBlocked, Mode=OneWay}\"");
        StringAssert.Contains(xaml, "Message=\"{x:Bind ViewModel.FileSaveBlockWarning, Mode=OneWay}\"");
        StringAssert.Contains(xaml, "Severity=\"Warning\"");
    }

    private static string ViewModelSource()
        => File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "ViewModels", "DeviceDetailViewModel.cs"));

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

    private static byte[] BuildGetFilePayload(string path, byte[] content)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        byte[] payload = new byte[2 + pathBytes.Length + 2 + content.Length];

        payload[0] = (byte)(pathBytes.Length & 0xFF);
        payload[1] = (byte)(pathBytes.Length >> 8);
        pathBytes.CopyTo(payload, 2);

        int contentLenOffset = 2 + pathBytes.Length;
        payload[contentLenOffset] = (byte)(content.Length & 0xFF);
        payload[contentLenOffset + 1] = (byte)(content.Length >> 8);
        content.CopyTo(payload, contentLenOffset + 2);

        return payload;
    }
}

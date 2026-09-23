namespace AnimatronicsControlCenter.Core.Protocol;

/// <summary>
/// 장치가 CMD_GET_FILE 응답을 조용히 잘라 보냈는지 판정한다.
///
/// 펌웨어는 파일 내용을 APP_CONTENT_MAX_LEN 고정 버퍼로 읽고 NUL 1바이트를 뺀 만큼만
/// 실어 보내면서도 BIN_STATUS_OK 를 반환한다. 잘렸다는 신호가 응답 어디에도 없으므로,
/// 파일 목록(CMD_GET_FILES)이 알려준 크기와 비교하는 방법뿐이다.
///
/// 버퍼를 키워도(512 → 2048) 그 한계를 넘는 파일에는 같은 일이 벌어지므로 이 검사는 계속 필요하다.
/// </summary>
public static class FirmwareFileTruncationCheck
{
    public readonly record struct TruncationResult(
        bool IsTruncated,
        long DeviceFileSize,
        int ReceivedBytes,
        string WarningMessage);

    /// <param name="deviceFileSize">CMD_GET_FILES 가 보고한 장치상의 파일 크기. 0 이하면 판정하지 않는다.</param>
    /// <param name="receivedBytes">
    /// 응답에 실제로 실려 온 content 바이트 수. 반드시 와이어의 content_len 값을 쓴다 —
    /// 디코딩된 문자열을 다시 UTF-8 로 인코딩한 길이는 멀티바이트 문자가 중간에서 잘렸을 때
    /// U+FFFD 치환 때문에 늘어나서 절단을 놓친다.
    /// </param>
    public static TruncationResult Inspect(long deviceFileSize, int receivedBytes)
    {
        // size 0 은 "빈 파일"과 "크기를 보고하지 않는 펌웨어"를 구분할 수 없어 판정 대상에서 뺀다.
        if (deviceFileSize <= 0 || receivedBytes >= deviceFileSize)
        {
            return new TruncationResult(false, deviceFileSize, receivedBytes, string.Empty);
        }

        return new TruncationResult(
            IsTruncated: true,
            DeviceFileSize: deviceFileSize,
            ReceivedBytes: receivedBytes,
            WarningMessage:
                $"장치가 파일을 잘라서 보냈습니다. {deviceFileSize}바이트 중 {receivedBytes}바이트만 수신했습니다. " +
                $"펌웨어 읽기/쓰기 한계가 {BinaryProtocolConst.MaxContentUtf8Bytes}바이트라 이 파일은 이 앱에서 " +
                "온전히 저장할 수 없습니다. 저장하면 수신하지 못한 뒷부분이 장치에서 사라집니다.");
    }
}

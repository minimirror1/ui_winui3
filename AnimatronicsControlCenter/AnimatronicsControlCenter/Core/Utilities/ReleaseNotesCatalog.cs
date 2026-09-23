using System;
using System.Collections.Generic;
using System.Linq;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Utilities;

/// <summary>
/// 버전별 릴리즈 노트 모음. 최신 버전이 먼저 온다.
///
/// 내용은 각 SW 버전 커밋과 그 사이 커밋들을 근거로 적었다. 새 버전을 낼 때는
/// Package.appxmanifest 를 올리면서 여기에도 항목을 더해야 한다.
/// 빠뜨리면 LatestMatchesManifest 테스트가 실패한다.
/// </summary>
public static class ReleaseNotesCatalog
{
    // Segoe Fluent Icons 글리프
    private const string GlyphShield = "";
    private const string GlyphSync = "";
    private const string GlyphWarning = "";
    private const string GlyphFile = "";
    private const string GlyphPlay = "";
    private const string GlyphRepeat = "";
    private const string GlyphSettings = "";
    private const string GlyphTheme = "";
    private const string GlyphFilter = "";
    private const string GlyphPower = "";
    private const string GlyphClock = "";
    private const string GlyphCloud = "";
    private const string GlyphView = "";
    private const string GlyphError = "";
    private const string GlyphPlug = "";
    private const string GlyphEdit = "";

    public static IReadOnlyList<ReleaseNote> All { get; } =
    [
        new("1.1.17.0", "2026-09-23", "파일을 잘림 없이 주고받습니다.",
        [
            new(GlyphWarning, "잘린 파일로 덮어쓰지 않습니다",
                "장치가 파일을 잘라서 보내면 경고를 띄우고 저장을 막습니다. 예전에는 잘린 내용을 그대로 저장해 장치의 원본 뒷부분이 사라졌습니다."),
            new(GlyphFile, "한 번에 보낼 수 있는 크기가 4배로",
                "설정 파일을 512바이트까지만 주고받던 것을 2048바이트로 늘렸습니다. 장치 쪽 펌웨어도 함께 올려야 적용됩니다."),
        ]),

        new("1.1.16.0", "2026-09-23", "장치에 정확한 시각을 보냅니다.",
        [
            new(GlyphClock, "인터넷 표준시에 맞춥니다",
                "시작할 때와 한 시간마다 인터넷 시간 서버에서 시각을 받아옵니다. PC 시계는 바꾸지 않고 차이만 기억했다가 장치에 보낼 때 반영합니다. 받아오지 못하면 PC 시계를 씁니다."),
            new(GlyphSync, "서머타임이 자동으로 반영됩니다",
                "국가별 고정 시차 대신 지역 이름으로 시간대를 고릅니다. 서머타임이 시작되고 끝나는 날짜가 자동으로 적용됩니다."),
        ]),

        new("1.1.15.0", "2026-07-16", "서버 연결에 인증이 생겼습니다.",
        [
            new(GlyphShield, "API 키로 서버에 인증합니다",
                "서버에 보내는 요청마다 키를 함께 보냅니다. 키는 윈도우 자격 증명 저장소에 보관합니다."),
            new(GlyphSettings, "키가 없으면 시작할 때 물어봅니다",
                "키가 저장돼 있지 않으면 앱을 켤 때 입력 창이 뜹니다."),
        ]),

        new("1.1.14.0", "2026-07-16", "장치 오류를 화면에서 바로 봅니다.",
        [
            new(GlyphError, "오류 상태 표시",
                "장치에 오류가 있으면 대시보드와 장치 상세 화면에 함께 표시합니다."),
            new(GlyphSettings, "오류 해제 버튼",
                "장치 상세 화면에서 오류 상태를 지울 수 있습니다."),
            new(GlyphPower, "Power 탭 한글 표기",
                "전원 탭의 영문 라벨을 한글로 바꿨습니다."),
        ]),

        new("1.1.11.0", "2026-06-18", "모션을 반복 재생할 수 있습니다.",
        [
            new(GlyphRepeat, "반복 재생",
                "장치 상세 화면에서 모션을 반복 재생합니다."),
            new(GlyphCloud, "운영시간을 서버에 저장",
                "장치별 운영시간 일정을 서버에 올려 둡니다."),
            new(GlyphSync, "주기 확인을 한 곳에서",
                "장치 상태를 주기적으로 묻는 동작이 화면마다 따로 돌던 것을 한 곳으로 모았습니다."),
        ]),

        new("1.1.10.0", "2026-05-20", "운영시간 관리 화면이 정리됐습니다.",
        [
            new(GlyphClock, "운영시간 전송 형식 변경",
                "장치에 보내는 운영시간 데이터 구조를 새 형식으로 바꿨습니다."),
            new(GlyphView, "일정과 휴일 화면 정리",
                "일정 표시, 휴일 항목, 장치 동작 버튼의 배치를 다시 잡았습니다."),
            new(GlyphSync, "서버 값과 비교",
                "장치에 들어 있는 값과 서버 값의 차이를 화면에서 견줘 볼 수 있습니다."),
        ]),

        new("1.1.9.2", "2026-05-18", "화면 테마를 고를 수 있습니다.",
        [
            new(GlyphTheme, "밝게 / 어둡게 / 시스템 설정",
                "설정에서 세 가지 중 고릅니다."),
            new(GlyphSettings, "변경 후 재시작 안내",
                "테마를 바꾸면 다시 시작해야 완전히 적용된다고 알려 줍니다."),
        ]),

        new("1.1.9.1", "2026-05-18", "시리얼 모니터가 훨씬 쓸 만해졌습니다.",
        [
            new(GlyphView, "상태 표시줄",
                "보낸 양, 받은 양, 합계, 실시간 수신 여부를 아래쪽에 표시합니다."),
            new(GlyphFilter, "방향과 장치로 걸러 보기",
                "보낸 것만, 받은 것만, 또는 특정 장치 번호만 골라 볼 수 있습니다."),
            new(GlyphFile, "시각과 방향, 바이트 수 열 추가",
                "각 줄에 시각, 방향 화살표, 바이트 수를 함께 보여 줍니다."),
        ]),

        new("1.1.9.0", "2026-05-15", "전원 릴레이를 직접 제어합니다.",
        [
            new(GlyphPower, "Power 탭에서 릴레이 제어",
                "흩어져 있던 릴레이 버튼을 전원 탭으로 모았습니다."),
            new(GlyphView, "대시보드 카드 정리",
                "장치 상태를 더 뚜렷하게 보여 주고, 카드에 마우스를 올리면 제어 버튼이 나옵니다."),
        ]),

        new("1.1.8.0", "2026-05-13", "연결이 자동으로 이어집니다.",
        [
            new(GlyphPlug, "이전 포트에 자동 연결",
                "마지막으로 쓰던 포트에 자동으로 붙고, 없으면 장치를 찾아봅니다."),
            new(GlyphCloud, "전원 상태를 실시간으로 받습니다",
                "서버가 전원 상태 변화를 바로 알려 줍니다. 앱이 주기적으로 묻지 않아도 됩니다."),
            new(GlyphSettings, "설정 파일 저장과 열기",
                "설정을 파일로 저장하고, 설정 창에서 그 파일을 바로 열 수 있습니다."),
        ]),

        new("1.1.7.2", "2026-05-12", "저장 경로와 자동 스크롤을 고쳤습니다.",
        [
            new(GlyphSettings, "서버 설정이 제자리에 저장됩니다",
                "서버 설정 파일이 엉뚱한 경로에 저장되던 문제를 고쳤습니다."),
            new(GlyphView, "시리얼 모니터 자동 스크롤",
                "새 내용이 들어오면 화면이 따라 내려갑니다."),
        ]),

        new("1.1.7.1", "2026-05-12", "서버 적용 버튼 위치를 고쳤습니다.",
        [
            new(GlyphEdit, "적용 버튼 배치 개선",
                "서버 값을 적용하는 버튼이 눈에 띄는 자리로 옮겨졌습니다."),
        ]),

        new("1.1.7.0", "2026-05-12", "백엔드 설정과 서버 모니터가 들어왔습니다.",
        [
            new(GlyphSettings, "백엔드 설정 화면 개편",
                "서버 접속 정보를 한 화면에서 관리합니다. 잠긴 항목이 무엇인지 표시합니다."),
            new(GlyphCloud, "서버 트래픽 표시기와 모니터",
                "서버와 주고받는 상황을 표시기로 보여 주고, 모니터 화면에서 자세히 볼 수 있습니다."),
            new(GlyphEdit, "오브제 매핑 편집",
                "장치와 서버 오브제의 연결을 화면에서 고칠 수 있습니다."),
            new(GlyphFile, "로컬 설정 파일 열기",
                "설정 파일이 있는 자리를 버튼 하나로 엽니다."),
        ]),
    ];

    /// 버전 문자열(예: "1.1.17.0")로 찾는다. 없으면 null.
    public static ReleaseNote? Find(string? version)
        => version is null ? null : All.FirstOrDefault(n => string.Equals(n.Version, version, StringComparison.Ordinal));

    /// 가장 최신 노트. 목록이 비는 경우는 없다.
    public static ReleaseNote Latest => All[0];
}

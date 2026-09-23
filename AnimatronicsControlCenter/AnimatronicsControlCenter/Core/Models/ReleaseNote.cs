using System.Collections.Generic;

namespace AnimatronicsControlCenter.Core.Models;

/// 릴리즈 노트 한 줄. 아이콘 하나와 제목, 설명으로 이루어진다.
public sealed record ReleaseNoteItem(string Glyph, string Title, string Body);

/// 버전 하나의 릴리즈 노트.
public sealed record ReleaseNote(
    string Version,
    string Date,
    string Headline,
    IReadOnlyList<ReleaseNoteItem> Items)
{
    /// 화면·태그·커밋 제목에서 쓰는 표기 (예: v1.1.17.0).
    public string DisplayVersion => $"v{Version}";
}

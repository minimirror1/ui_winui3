using System.Collections.Generic;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public partial class ReleaseNotesViewModel : ObservableObject
    {
        private readonly string? _installedVersion;

        /// 최신 버전이 먼저 온다.
        public IReadOnlyList<ReleaseNote> Releases { get; } = ReleaseNotesCatalog.All;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSelectedInstalled))]
        private ReleaseNote? selectedRelease;

        /// 고른 버전이 지금 실행 중인 버전인지.
        public bool IsSelectedInstalled =>
            SelectedRelease is not null && SelectedRelease.Version == _installedVersion;

        public ReleaseNotesViewModel()
        {
            _installedVersion = AppVersionProvider.Version;

            // 실행 중인 버전의 노트를 먼저 보여 준다. 없으면 가장 최신 것.
            SelectedRelease = ReleaseNotesCatalog.Find(_installedVersion)
                              ?? (Releases.Count > 0 ? Releases[0] : null);
        }
    }
}

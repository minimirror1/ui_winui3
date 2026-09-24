using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    /// 저장된 기록 파일을 열어 보는 화면. 기록하는 쪽과는 파일만 공유한다.
    public partial class LogViewerViewModel : ObservableObject
    {
        /// 한 번에 담을 수 있는 줄 수. 넘으면 조건을 더 좁히라고 알린다.
        public const int MaxRows = 5_000;

        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private DateOnly selectedDate = DateOnly.FromDateTime(DateTime.Now);

        /// "" 는 전체, 그 외에는 OK 나 ERROR.
        [ObservableProperty]
        private string selectedResult = string.Empty;

        [ObservableProperty]
        private string deviceFilterText = string.Empty;

        [ObservableProperty]
        private bool isTruncated;

        public LogViewerViewModel(ISettingsService settingsService)
            => _settingsService = settingsService;

        public ObservableCollection<LogRecordRow> Rows { get; } = new();

        public string LogFolder => LogFilePolicy.FolderFor(_settingsService.AppSettingsFilePath);

        [RelayCommand]
        public void Refresh()
        {
            LogReadResult result = LogFileReader.Read(LogFolder, SelectedDate, BuildFilter(), MaxRows);

            Rows.Clear();
            foreach (LogRecordRow row in result.Rows) Rows.Add(row);
            IsTruncated = result.IsTruncated;
        }

        private LogFilter BuildFilter()
        {
            int? device = int.TryParse(DeviceFilterText, NumberStyles.None, CultureInfo.InvariantCulture, out int id)
                ? id
                : null;

            return new LogFilter(device, string.IsNullOrWhiteSpace(SelectedResult) ? null : SelectedResult);
        }
    }
}

using System;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.Helpers;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class LogViewerPage : Page
{
    public LogViewerPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<LogViewerViewModel>();
        Strings = new LocalizedStrings(App.Current.Services.GetRequiredService<ILocalizationService>());

        InitializeComponent();

        DatePicker.Date = ViewModel.SelectedDate.ToDateTime(TimeOnly.MinValue);
        ViewModel.Refresh();
    }

    public LogViewerViewModel ViewModel { get; }

    public LocalizedStrings Strings { get; }

    private void OnDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate is not DateTimeOffset picked) return;

        ViewModel.SelectedDate = DateOnly.FromDateTime(picked.Date);
        ViewModel.Refresh();
    }

    private void OnResultChanged(object sender, SelectionChangedEventArgs e)
    {
        // 첫 항목은 "전체"라 조건을 걸지 않는다.
        ViewModel.SelectedResult = ResultBox.SelectedIndex switch
        {
            1 => "OK",
            2 => "ERROR",
            _ => string.Empty,
        };

        ViewModel.Refresh();
    }
}

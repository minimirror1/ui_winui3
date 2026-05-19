using System;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class OperatingHoursSyncPage : Page
{
    private readonly OperatingHoursCompareDialog _compareDialog;
    private DispatcherQueueTimer? _countdownTimer;
    private int _countdownRemaining;

    public OperatingHoursSyncPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<OperatingHoursSyncViewModel>();
        _compareDialog = new OperatingHoursCompareDialog(ViewModel);

        ViewModel.CompareRequested       += OnCompareRequested;
        ViewModel.CountdownStartRequested += OnCountdownStartRequested;
    }

    public OperatingHoursSyncViewModel ViewModel { get; }

    private async void OnCompareRequested(object? sender, EventArgs e)
    {
        _compareDialog.XamlRoot = XamlRoot;
        await _compareDialog.ShowAsync();
    }

    private void OnCountdownStartRequested(object? sender, int seconds)
    {
        _countdownRemaining        = seconds;
        ViewModel.ShowAutoSyncBadge = true;
        ViewModel.AutoSyncBadgeText = $"서버 자동 동기화 {seconds}초 전";

        _countdownTimer?.Stop();
        var timer        = DispatcherQueue.CreateTimer();
        timer.Interval   = TimeSpan.FromSeconds(1);
        timer.Tick      += (_, _) =>
        {
            _countdownRemaining--;
            if (_countdownRemaining <= 0)
            {
                ViewModel.ShowAutoSyncBadge = false;
                timer.Stop();
                return;
            }
            ViewModel.AutoSyncBadgeText = $"서버 자동 동기화 {_countdownRemaining}초 전";
        };
        _countdownTimer = timer;
        timer.Start();
    }
}

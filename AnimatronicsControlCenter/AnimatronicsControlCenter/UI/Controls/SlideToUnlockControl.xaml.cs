using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;

namespace AnimatronicsControlCenter.UI.Controls
{
    public sealed partial class SlideToUnlockControl : UserControl
    {
        public static readonly DependencyProperty IsUnlockedProperty =
            DependencyProperty.Register(nameof(IsUnlocked), typeof(bool), typeof(SlideToUnlockControl),
                new PropertyMetadata(false, OnIsUnlockedChanged));

        public bool IsUnlocked
        {
            get => (bool)GetValue(IsUnlockedProperty);
            set => SetValue(IsUnlockedProperty, value);
        }

        public event EventHandler? Unlocked;

        private bool _dragging;
        private double _dragStartX;
        private double _thumbStartX = 4.0;
        private double _maxThumbX = 260.0;

        private Storyboard? _sweepStoryboard;

        public SlideToUnlockControl()
        {
            this.InitializeComponent();
            ActualThemeChanged += (_, _) => ApplyUnlockedState(IsUnlocked);
            StartSweepAnimation();
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _maxThumbX = Math.Max(4.0, e.NewSize.Width - 48 - 4);
        }

        private static void OnIsUnlockedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SlideToUnlockControl ctrl)
                ctrl.ApplyUnlockedState((bool)e.NewValue);
        }

        private void ApplyUnlockedState(bool unlocked)
        {
            if (unlocked)
            {
                _sweepStoryboard?.Stop();
                ShimmerHost.Visibility = Visibility.Collapsed;
                Canvas.SetLeft(ThumbBorder, _maxThumbX);
                TrackBorder.Background = GetBrush("SlideUnlockTrackBackgroundBrush");
                TrackBorder.BorderBrush = GetBrush("SlideUnlockTrackBorderBrush");
                HintText.Text = "잠금 해제됨 — 릴레이 제어 가능";
                HintText.Foreground = GetBrush("SlideUnlockHintBrush");
                ThumbIcon.Glyph = "";
            }
            else
            {
                Canvas.SetLeft(ThumbBorder, 4.0);
                _thumbStartX = 4.0;
                TrackBorder.Background = GetBrush("SlideLockTrackBackgroundBrush");
                TrackBorder.BorderBrush = GetBrush("SlideLockTrackBorderBrush");
                HintText.Text = "▶ 밀어서 잠금 해제";
                HintText.Foreground = GetBrush("SlideLockHintBrush");
                ThumbIcon.Glyph = "";
                ShimmerHost.Visibility = Visibility.Visible;
                StartSweepAnimation();
            }
        }

        private void StartSweepAnimation()
        {
            if (_sweepStoryboard != null)
            {
                _sweepStoryboard.Stop();
                _sweepStoryboard = null;
            }

            var anim = new DoubleAnimation
            {
                From = -180,
                To = 460,
                Duration = new Duration(TimeSpan.FromSeconds(2.4)),
                RepeatBehavior = RepeatBehavior.Forever,
                EnableDependentAnimation = true,
            };
            Storyboard.SetTarget(anim, SweepTransform);
            Storyboard.SetTargetProperty(anim, "X");

            _sweepStoryboard = new Storyboard();
            _sweepStoryboard.Children.Add(anim);
            _sweepStoryboard.Begin();
        }

        private SolidColorBrush GetBrush(string resourceKey)
            => ResolveThemeBrush(resourceKey);

        private SolidColorBrush ResolveThemeBrush(string resourceKey)
        {
            string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
            if (Resources.ThemeDictionaries.TryGetValue(themeKey, out object? themeResources) &&
                themeResources is ResourceDictionary themeDictionary &&
                themeDictionary.TryGetValue(resourceKey, out object? brush) &&
                brush is SolidColorBrush resolvedBrush)
            {
                return resolvedBrush;
            }

            return (SolidColorBrush)Resources[resourceKey];
        }

        private void Thumb_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (IsUnlocked) return;
            _dragging = true;
            _thumbStartX = Canvas.GetLeft(ThumbBorder);
            _dragStartX = e.GetCurrentPoint(RootGrid).Position.X;
            ThumbBorder.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Thumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging) return;
            double delta = e.GetCurrentPoint(RootGrid).Position.X - _dragStartX;
            double newX = Math.Clamp(_thumbStartX + delta, 4.0, _maxThumbX);
            Canvas.SetLeft(ThumbBorder, newX);

            double pct = _maxThumbX > 4.0 ? (newX - 4.0) / (_maxThumbX - 4.0) : 0;
            HintText.Opacity = 1.0 - pct * 0.85;
            e.Handled = true;
        }

        private void Thumb_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            ThumbBorder.ReleasePointerCapture(e.Pointer);

            double currentX = Canvas.GetLeft(ThumbBorder);
            if (currentX >= _maxThumbX - 6)
            {
                Canvas.SetLeft(ThumbBorder, _maxThumbX);
                Unlocked?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Canvas.SetLeft(ThumbBorder, 4.0);
                HintText.Opacity = 1.0;
            }
            e.Handled = true;
        }

        private void Thumb_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                Canvas.SetLeft(ThumbBorder, 4.0);
                HintText.Opacity = 1.0;
            }
        }
    }
}

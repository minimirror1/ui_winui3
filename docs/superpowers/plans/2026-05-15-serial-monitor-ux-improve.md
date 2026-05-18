# Serial Monitor UX Improve Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 시리얼 모니터 화면을 디자인 파일(`winui3-ui-ux` 번들)에 맞게 개선한다 — 시리얼 행 방향 배지(↑/↓), TX/RX 컬러 업데이트, 패킷 탭 방향·송신ID·수신ID 필터 추가, 하단 상태바 추가.

**Architecture:** `SerialTrafficEntry` 모델에 `DirectionArrow` 속성 추가 → 컨버터 색상 업데이트 → ViewModel에 3개 필터 속성 추가 → XAML DataTemplate/필터 행/상태바 개선. 기존 아키텍처(ViewModel↔XAML x:Bind)를 그대로 따름.

**Tech Stack:** WinUI3, CommunityToolkit.Mvvm, C# record, XAML x:Bind, MSTest (file-based assertions)

---

## File Map

| 작업 | 파일 |
|---|---|
| Modify | `AnimatronicsControlCenter/Core/Models/SerialTrafficEntry.cs` |
| Modify | `AnimatronicsControlCenter/UI/Converters/SerialDirectionToBrushConverter.cs` |
| Modify | `AnimatronicsControlCenter/UI/ViewModels/SerialMonitorViewModel.cs` |
| Modify | `AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml` |
| Modify | `AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml.cs` |
| Create | `AnimatronicsControlCenter.Tests/SerialMonitorImproveTests.cs` |

---

### Task 1: SerialTrafficEntry에 DirectionArrow 추가 + 브러시 색상 업데이트

**Files:**
- Modify: `AnimatronicsControlCenter/Core/Models/SerialTrafficEntry.cs`
- Modify: `AnimatronicsControlCenter/UI/Converters/SerialDirectionToBrushConverter.cs`

- [ ] **Step 1: SerialTrafficEntry.cs에 DirectionArrow 속성 추가**

`Line` 속성 아래에 추가:

```csharp
public string DirectionArrow => Direction == SerialTrafficDirection.Tx ? "↑" : "↓";
```

완성된 레코드:
```csharp
public sealed record SerialTrafficEntry(
    DateTimeOffset Timestamp,
    SerialTrafficDirection Direction,
    string Line)
{
    public string Prefix => Direction == SerialTrafficDirection.Tx ? ">" : "<";

    public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

    public string DirectionArrow => Direction == SerialTrafficDirection.Tx ? "↑" : "↓";

    public string DisplayLine => $"{Prefix}[{TimestampText}]{Line}";
}
```

- [ ] **Step 2: SerialDirectionToBrushConverter.cs 색상 업데이트**

디자인 명세: TX = 파란색(outbound), RX = 초록색(inbound).
기존 코드는 반대(TX=LimeGreen, RX=DeepSkyBlue). 수정:

```csharp
public sealed class SerialDirectionToBrushConverter : IValueConverter
{
    // TX → 파란색(송신), RX → 초록색(수신) — 디자인 명세에 따름
    private static readonly SolidColorBrush TxBrush = new(Windows.UI.Color.FromArgb(255, 107, 163, 214));
    private static readonly SolidColorBrush RxBrush = new(Windows.UI.Color.FromArgb(255, 110, 196, 160));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is SerialTrafficDirection dir)
        {
            return dir == SerialTrafficDirection.Tx ? TxBrush : RxBrush;
        }
        return RxBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 3: 테스트 파일 생성 및 실행**

`AnimatronicsControlCenter.Tests/SerialMonitorImproveTests.cs` 파일 생성:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class SerialMonitorImproveTests
{
    // ── Task 1 ──────────────────────────────────────────────
    [TestMethod]
    public void SerialTrafficEntry_HasDirectionArrow()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "Core", "Models", "SerialTrafficEntry.cs"));

        StringAssert.Contains(code, "DirectionArrow");
        StringAssert.Contains(code, "\"↑\"");
        StringAssert.Contains(code, "\"↓\"");
    }

    [TestMethod]
    public void SerialDirectionBrushConverter_UsesTxBlueRxGreen()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Converters", "SerialDirectionToBrushConverter.cs"));

        // TX = 107,163,214 (blue)
        StringAssert.Contains(code, "107, 163, 214");
        // RX = 110,196,160 (green)
        StringAssert.Contains(code, "110, 196, 160");
    }

    // ── Task 2 ──────────────────────────────────────────────
    [TestMethod]
    public void SerialPage_DataTemplate_ShowsDirectionArrowAndTimestamp()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Views", "SerialMonitorPage.xaml"));

        StringAssert.Contains(xaml, "DirectionArrow");
        StringAssert.Contains(xaml, "TimestampText");
        // x:Bind Line (not DisplayLine) for bytes column
        StringAssert.Contains(xaml, "x:Bind Line");
    }

    // ── Task 3 ──────────────────────────────────────────────
    [TestMethod]
    public void SerialMonitorViewModel_HasPacketDirectionFilter()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "ViewModels", "SerialMonitorViewModel.cs"));

        StringAssert.Contains(code, "PacketDirectionFilters");
        StringAssert.Contains(code, "selectedPacketDirectionFilter");
        StringAssert.Contains(code, "PacketSrcIdFilters");
        StringAssert.Contains(code, "selectedPacketSrcIdFilter");
        StringAssert.Contains(code, "PacketTarIdFilters");
        StringAssert.Contains(code, "selectedPacketTarIdFilter");
    }

    [TestMethod]
    public void SerialMonitorViewModel_MatchesPacketFilter_UsesDirectionFilter()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "ViewModels", "SerialMonitorViewModel.cs"));

        StringAssert.Contains(code, "SelectedPacketDirectionFilter");
        StringAssert.Contains(code, "↑ 송신");
        StringAssert.Contains(code, "↓ 수신");
        StringAssert.Contains(code, "SelectedPacketSrcIdFilter");
        StringAssert.Contains(code, "SelectedPacketTarIdFilter");
    }

    // ── Task 4 ──────────────────────────────────────────────
    [TestMethod]
    public void SerialPage_PacketTab_HasDirectionSrcTarFilterControls()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Views", "SerialMonitorPage.xaml"));

        StringAssert.Contains(xaml, "PacketDirectionFilters");
        StringAssert.Contains(xaml, "SelectedPacketDirectionFilter");
        StringAssert.Contains(xaml, "PacketSrcIdFilters");
        StringAssert.Contains(xaml, "SelectedPacketSrcIdFilter");
        StringAssert.Contains(xaml, "PacketTarIdFilters");
        StringAssert.Contains(xaml, "SelectedPacketTarIdFilter");
    }

    // ── Task 5 ──────────────────────────────────────────────
    [TestMethod]
    public void SerialPage_HasStatusBar()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Views", "SerialMonitorPage.xaml"));

        // Status bar must show TX, RX, LIVE/Paused
        StringAssert.Contains(xaml, "statusbar");
        StringAssert.Contains(xaml, "ViewModel.TxCount");
        StringAssert.Contains(xaml, "ViewModel.RxCount");
        StringAssert.Contains(xaml, "ViewModel.IsPaused");
    }

    private static string ProjectPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        Assert.Fail($"Could not find project file: {Path.Combine(segments)}");
        return string.Empty;
    }
}
```

- [ ] **Step 4: 테스트 실행 (Task 1 테스트만 통과 확인)**

```
cd AnimatronicsControlCenter.Tests
dotnet test --filter "SerialMonitorImproveTests" --no-build 2>&1 | head -30
```

Task 1 테스트 2개(SerialTrafficEntry_HasDirectionArrow, SerialDirectionBrushConverter_UsesTxBlueRxGreen)만 통과해야 함. 나머지는 아직 실패.

- [ ] **Step 5: 커밋**

```bash
git add AnimatronicsControlCenter/Core/Models/SerialTrafficEntry.cs
git add AnimatronicsControlCenter/UI/Converters/SerialDirectionToBrushConverter.cs
git add AnimatronicsControlCenter.Tests/SerialMonitorImproveTests.cs
git commit -m "feat: add DirectionArrow to SerialTrafficEntry, update TX/RX brush colors"
```

---

### Task 2: 시리얼/ComRaw 탭 DataTemplate 개선

**Files:**
- Modify: `AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml`

현재 단일 TextBlock → 타임스탬프·방향배지·바이트 3열 Grid.

- [ ] **Step 1: 시리얼 탭 DataTemplate 변경**

`SerialMonitorPage.xaml`의 시리얼 탭 `ItemsRepeater.ItemTemplate` 내부 DataTemplate을 다음으로 교체:

```xml
<DataTemplate x:DataType="models:SerialTrafficEntry" xmlns:models="using:AnimatronicsControlCenter.Core.Models">
    <Grid ColumnSpacing="8" Padding="0,1">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="86"/>
            <ColumnDefinition Width="14"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <TextBlock Grid.Column="0"
                   Text="{x:Bind TimestampText}"
                   FontFamily="Consolas"
                   FontSize="12"
                   Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}"
                   TextWrapping="NoWrap"/>
        <TextBlock Grid.Column="1"
                   Text="{x:Bind DirectionArrow}"
                   FontFamily="Consolas"
                   FontSize="12"
                   FontWeight="SemiBold"
                   Foreground="{Binding Direction, Converter={StaticResource SerialDirectionToBrushConverter}}"
                   TextWrapping="NoWrap"/>
        <TextBlock Grid.Column="2"
                   Text="{x:Bind Line}"
                   FontFamily="Consolas"
                   FontSize="12"
                   Foreground="{Binding Direction, Converter={StaticResource SerialDirectionToBrushConverter}}"
                   TextWrapping="NoWrap"/>
    </Grid>
</DataTemplate>
```

- [ ] **Step 2: ComRaw 탭 DataTemplate 동일하게 변경**

ComRaw 탭 `ItemsRepeater.ItemTemplate` DataTemplate도 Step 1과 동일하게 교체 (동일한 구조, 동일한 리소스 선언이 이미 ComRaw ScrollViewer.Resources에 있음).

- [ ] **Step 3: 테스트 실행**

```
dotnet test --filter "SerialPage_DataTemplate_ShowsDirectionArrowAndTimestamp"
```

Expected: PASS

- [ ] **Step 4: 커밋**

```bash
git add AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml
git commit -m "feat: update serial/comRaw DataTemplate with timestamp, direction arrow, bytes columns"
```

---

### Task 3: ViewModel에 패킷 방향·송신ID·수신ID 필터 추가

**Files:**
- Modify: `AnimatronicsControlCenter/UI/ViewModels/SerialMonitorViewModel.cs`

- [ ] **Step 1: 필터 컬렉션 + ObservableProperty 추가**

`PacketCommandFilters` 선언 바로 아래에 추가:

```csharp
public ObservableCollection<string> PacketDirectionFilters { get; } = new() { "전체", "↑ 송신", "↓ 수신" };
public ObservableCollection<string> PacketSrcIdFilters { get; } = new() { "전체" };
public ObservableCollection<string> PacketTarIdFilters { get; } = new() { "전체" };
```

`[ObservableProperty] private string selectedPacketCommandFilter = "All";` 아래에 추가:

```csharp
[ObservableProperty]
private string selectedPacketDirectionFilter = "전체";

[ObservableProperty]
private string selectedPacketSrcIdFilter = "전체";

[ObservableProperty]
private string selectedPacketTarIdFilter = "전체";
```

- [ ] **Step 2: partial 메서드 추가 (필터 변경 시 목록 재구성)**

`OnIsParseErrorOnlyChanged` 아래에 추가:

```csharp
partial void OnSelectedPacketDirectionFilterChanged(string value)
{
    RebuildVisible();
}

partial void OnSelectedPacketSrcIdFilterChanged(string value)
{
    RebuildVisible();
}

partial void OnSelectedPacketTarIdFilterChanged(string value)
{
    RebuildVisible();
}
```

- [ ] **Step 3: AddPacketIdFilters 헬퍼 추가**

`AddPacketCommandFilter` 메서드 아래에 추가:

```csharp
private void AddPacketSrcIdFilter(int? srcId)
{
    if (!srcId.HasValue) return;
    var s = srcId.Value.ToString();
    if (!PacketSrcIdFilters.Contains(s)) PacketSrcIdFilters.Add(s);
}

private void AddPacketTarIdFilter(int? tarId)
{
    if (!tarId.HasValue) return;
    var s = tarId.Value.ToString();
    if (!PacketTarIdFilters.Contains(s)) PacketTarIdFilters.Add(s);
}
```

- [ ] **Step 4: AppendToAll에서 새 헬퍼 호출**

기존 `AppendToAll` 내 `AddPacketCommandFilter(packet.Command);` 줄 아래에 추가:

```csharp
AddPacketSrcIdFilter(packet.SrcId);
AddPacketTarIdFilter(packet.TarId);
```

- [ ] **Step 5: MatchesPacketFilter 업데이트**

기존 메서드 전체를 다음으로 교체:

```csharp
private bool MatchesPacketFilter(PacketItem packet)
{
    if (IsParseErrorOnly && packet.ParseError == null) return false;
    if (SelectedPacketCommandFilter != "All" && packet.Command != SelectedPacketCommandFilter) return false;
    if (SelectedPacketStatusFilter != "All" && packet.Status != SelectedPacketStatusFilter) return false;
    if (SelectedPacketDirectionFilter == "↑ 송신" && packet.Traffic.Direction != SerialTrafficDirection.Tx) return false;
    if (SelectedPacketDirectionFilter == "↓ 수신" && packet.Traffic.Direction != SerialTrafficDirection.Rx) return false;
    if (SelectedPacketSrcIdFilter != "전체")
    {
        if (!int.TryParse(SelectedPacketSrcIdFilter, out var srcId) || packet.SrcId != srcId) return false;
    }
    if (SelectedPacketTarIdFilter != "전체")
    {
        if (!int.TryParse(SelectedPacketTarIdFilter, out var tarId) || packet.TarId != tarId) return false;
    }
    return true;
}
```

Note: `MatchesFilter(packet.Traffic)` 호출을 제거함. 패킷 탭은 전용 방향 필터(`SelectedPacketDirectionFilter`)를 사용.

- [ ] **Step 6: Clear() 업데이트**

`Clear()` 메서드의 `PacketCommandFilters.Clear(); PacketCommandFilters.Add("All");` 아래에 추가:

```csharp
PacketSrcIdFilters.Clear();
PacketSrcIdFilters.Add("전체");
PacketTarIdFilters.Clear();
PacketTarIdFilters.Add("전체");
SelectedPacketDirectionFilter = "전체";
SelectedPacketSrcIdFilter = "전체";
SelectedPacketTarIdFilter = "전체";
```

- [ ] **Step 7: 테스트 실행**

```
dotnet test --filter "SerialMonitorViewModel_HasPacketDirectionFilter|SerialMonitorViewModel_MatchesPacketFilter_UsesDirectionFilter"
```

Expected: 2개 PASS

- [ ] **Step 8: 커밋**

```bash
git add AnimatronicsControlCenter/UI/ViewModels/SerialMonitorViewModel.cs
git commit -m "feat: add direction/srcId/tarId packet filters to SerialMonitorViewModel"
```

---

### Task 4: 패킷 탭 필터 행에 방향·송신ID·수신ID 컨트롤 추가

**Files:**
- Modify: `AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml`

- [ ] **Step 1: 패킷 탭 필터 StackPanel 수정**

현재 패킷 탭 필터 StackPanel (`Grid.Row="0" Grid.ColumnSpan="2"`):

```xml
<StackPanel Grid.Row="0" Grid.ColumnSpan="2" Orientation="Horizontal" Spacing="12">
    <TextBlock Text="{x:Bind ViewModel.Strings.Get('SerialMonitor_ParseErrors', ViewModel.Strings.Code), Mode=OneWay}"/>
    <TextBlock Text="{x:Bind ViewModel.ParseErrorCount, Mode=OneWay}"/>
    <TextBlock Text="Command" VerticalAlignment="Center"/>
    <ComboBox ItemsSource="{x:Bind ViewModel.PacketCommandFilters, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedPacketCommandFilter, Mode=TwoWay}"
              MinWidth="140"/>
    <TextBlock Text="Status" VerticalAlignment="Center"/>
    <ComboBox ItemsSource="{x:Bind ViewModel.PacketStatusFilters, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedPacketStatusFilter, Mode=TwoWay}"
              MinWidth="100"/>
    <ToggleSwitch Header="Errors only"
                  IsOn="{x:Bind ViewModel.IsParseErrorOnly, Mode=TwoWay}"/>
</StackPanel>
```

다음으로 교체 (방향·송신ID·수신ID 필터를 앞에 추가):

```xml
<StackPanel Grid.Row="0" Grid.ColumnSpan="2" Orientation="Horizontal" Spacing="12">
    <TextBlock Text="{x:Bind ViewModel.Strings.Get('SerialMonitor_ParseErrors', ViewModel.Strings.Code), Mode=OneWay}"
               VerticalAlignment="Center"/>
    <TextBlock Text="{x:Bind ViewModel.ParseErrorCount, Mode=OneWay}"
               VerticalAlignment="Center"/>
    <TextBlock Text="방향" VerticalAlignment="Center"/>
    <ComboBox ItemsSource="{x:Bind ViewModel.PacketDirectionFilters, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedPacketDirectionFilter, Mode=TwoWay}"
              MinWidth="100"/>
    <TextBlock Text="송신 ID" VerticalAlignment="Center"/>
    <ComboBox ItemsSource="{x:Bind ViewModel.PacketSrcIdFilters, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedPacketSrcIdFilter, Mode=TwoWay}"
              MinWidth="80"/>
    <TextBlock Text="수신 ID" VerticalAlignment="Center"/>
    <ComboBox ItemsSource="{x:Bind ViewModel.PacketTarIdFilters, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedPacketTarIdFilter, Mode=TwoWay}"
              MinWidth="80"/>
    <TextBlock Text="Command" VerticalAlignment="Center"/>
    <ComboBox ItemsSource="{x:Bind ViewModel.PacketCommandFilters, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedPacketCommandFilter, Mode=TwoWay}"
              MinWidth="140"/>
    <TextBlock Text="Status" VerticalAlignment="Center"/>
    <ComboBox ItemsSource="{x:Bind ViewModel.PacketStatusFilters, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedPacketStatusFilter, Mode=TwoWay}"
              MinWidth="100"/>
    <ToggleSwitch Header="오류만"
                  IsOn="{x:Bind ViewModel.IsParseErrorOnly, Mode=TwoWay}"/>
</StackPanel>
```

- [ ] **Step 2: 테스트 실행**

```
dotnet test --filter "SerialPage_PacketTab_HasDirectionSrcTarFilterControls"
```

Expected: PASS

- [ ] **Step 3: 커밋**

```bash
git add AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml
git commit -m "feat: add direction/srcId/tarId filter controls to packet tab"
```

---

### Task 5: 하단 상태바 추가

**Files:**
- Modify: `AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml`
- Modify: `AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml.cs`

- [ ] **Step 1: Page의 루트 Grid에 4번째 행 추가**

`SerialMonitorPage.xaml`의 루트 Grid:

기존:
```xml
<Grid Padding="12" RowSpacing="8">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
    </Grid.RowDefinitions>
```

다음으로 교체:
```xml
<Grid Padding="12,12,12,0" RowSpacing="8">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
```

- [ ] **Step 2: 상태바 XAML 추가**

`</Pivot>` 닫는 태그 바로 뒤에 추가 (Grid Row 3):

```xml
<!-- Status bar -->
<Border x:Name="statusbar"
        Grid.Row="3"
        Background="{ThemeResource CardBackgroundFillColorSecondaryBrush}"
        BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
        BorderThickness="0,1,0,0"
        Padding="12,5"
        Margin="-12,0,-12,0">
    <StackPanel Orientation="Horizontal" Spacing="16">
        <StackPanel Orientation="Horizontal" Spacing="6" VerticalAlignment="Center">
            <Ellipse Width="7" Height="7" Fill="#4CC180"/>
            <TextBlock Text="연결됨" FontSize="12"
                       Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}"/>
        </StackPanel>
        <TextBlock VerticalAlignment="Center" FontSize="12" FontFamily="Consolas"
                   Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}">
            <Run Text="TX "/>
            <Run Text="{x:Bind ViewModel.TxCount, Mode=OneWay}"
                 FontWeight="SemiBold"
                 Foreground="{ThemeResource SystemControlForegroundBaseHighBrush}"/>
        </TextBlock>
        <TextBlock VerticalAlignment="Center" FontSize="12" FontFamily="Consolas"
                   Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}">
            <Run Text="RX "/>
            <Run Text="{x:Bind ViewModel.RxCount, Mode=OneWay}"
                 FontWeight="SemiBold"
                 Foreground="{ThemeResource SystemControlForegroundBaseHighBrush}"/>
        </TextBlock>
        <TextBlock VerticalAlignment="Center" FontSize="12" FontFamily="Consolas"
                   Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}">
            <Run Text="Total "/>
            <Run Text="{x:Bind ViewModel.TotalCount, Mode=OneWay}"
                 FontWeight="SemiBold"
                 Foreground="{ThemeResource SystemControlForegroundBaseHighBrush}"/>
        </TextBlock>
        <!-- LIVE indicator (not paused) -->
        <StackPanel Orientation="Horizontal" Spacing="5" VerticalAlignment="Center"
                    Visibility="{x:Bind BoolToCollapsed(ViewModel.IsPaused), Mode=OneWay}">
            <Ellipse Width="6" Height="6" Fill="#4CC180">
                <Ellipse.RenderTransform>
                    <ScaleTransform/>
                </Ellipse.RenderTransform>
            </Ellipse>
            <TextBlock Text="LIVE" FontSize="12" FontWeight="SemiBold"
                       Foreground="#4CC180"/>
        </StackPanel>
        <!-- Paused indicator -->
        <TextBlock Text="● 일시정지"
                   FontSize="12" FontWeight="SemiBold"
                   Foreground="{ThemeResource SystemAccentColor}"
                   VerticalAlignment="Center"
                   Visibility="{x:Bind BoolToVisible(ViewModel.IsPaused), Mode=OneWay}"/>
    </StackPanel>
</Border>
```

- [ ] **Step 3: SerialMonitorPage.xaml.cs에 x:Bind 헬퍼 추가**

`DashboardPage.xaml.cs`와 동일한 패턴. `SerialMonitorPage` 클래스에 추가:

```csharp
private Visibility BoolToVisible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
private Visibility BoolToCollapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
```

`using` 문에 `Microsoft.UI.Xaml;`은 이미 있으므로 추가 불필요.

- [ ] **Step 4: 테스트 실행**

```
dotnet test --filter "SerialPage_HasStatusBar"
```

Expected: PASS

- [ ] **Step 5: 전체 테스트 실행**

```
dotnet test --filter "SerialMonitorImproveTests"
```

Expected: 모든 7개 테스트 PASS

- [ ] **Step 6: 커밋**

```bash
git add AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml
git add AnimatronicsControlCenter/UI/Views/SerialMonitorPage.xaml.cs
git commit -m "feat: add status bar with TX/RX/Total/LIVE indicators to serial monitor"
```

---

### Task 6: 전체 테스트 + 빌드 검증

- [ ] **Step 1: 전체 테스트 실행**

```
dotnet test AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj
```

Expected: 기존 테스트 포함 전체 통과. 실패 있으면 원인 확인 후 수정.

- [ ] **Step 2: 빌드 확인 (XAML 바인딩 오류 포함)**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter.csproj -c Debug
```

Expected: 0 errors. warning은 기존 코드에서 오는 것만 허용.

- [ ] **Step 3: 최종 커밋 (필요시)**

빌드/테스트 수정 사항이 있으면 커밋.

---

## 자가 검토

**스펙 커버리지:**
- ✅ A. 패킷 필터 (방향/송신ID/수신ID) — Task 3, 4
- ✅ B. 시리얼 행 개선 (방향 배지, TX/RX 컬러) — Task 1, 2
- ✅ C. 하단 상태바 — Task 5

**제외 항목 (디자인 도구 산물):**
- Tweaks 패널
- scale-to-fit 1440×860 고정
- 스파크라인 애니메이션

**타입/메서드명 일관성:**
- `PacketDirectionFilters` / `SelectedPacketDirectionFilter` — 일치
- `PacketSrcIdFilters` / `SelectedPacketSrcIdFilter` — 일치
- `PacketTarIdFilters` / `SelectedPacketTarIdFilter` — 일치
- `AddPacketSrcIdFilter(packet.SrcId)` → `packet.SrcId`는 `int?` — 일치
- `BoolToVisible` / `BoolToCollapsed` — SerialMonitorPage.xaml.cs에서 선언, XAML에서 호출

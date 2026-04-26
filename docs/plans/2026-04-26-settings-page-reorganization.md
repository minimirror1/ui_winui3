# Settings Page Reorganization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 설정 페이지를 "연결 설정 → 통신 설정 → 앱 설정" 3개 섹션으로 재구성하고, 가상 모드를 연결 그룹 안으로 이동하며, PING 설정을 SettingsCard → SettingsExpander로 전환하고, 하드코딩된 문자열을 모두 다국어 리소스로 교체한다.

**Architecture:** XAML 전용 재구성이며 ViewModel 로직 변경 없음. 섹션 구분은 `BodyStrongTextBlockStyle` TextBlock 헤더로 표현. 하드코딩 문자열은 기존 `.resw` 패턴(`Key.Property` 형식)을 따라 추가.

**Tech Stack:** WinUI 3, CommunityToolkit.WinUI.Controls (SettingsExpander / SettingsCard), RESW 다국어 리소스

---

## 파일 구조

| 파일 | 변경 내용 |
|------|-----------|
| `AnimatronicsControlCenter/AnimatronicsControlCenter/Strings/ko-KR/Resources.resw` | 누락된 한국어 문자열 12개 추가 |
| `AnimatronicsControlCenter/AnimatronicsControlCenter/Strings/en-US/Resources.resw` | 누락된 영어 문자열 12개 추가 |
| `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/SettingsPage.xaml` | 전체 구조 재작성 |

---

## Task 1: 다국어 리소스 문자열 추가

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Strings/ko-KR/Resources.resw`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Strings/en-US/Resources.resw`

### 추가할 문자열 목록

| Key | ko-KR | en-US |
|-----|-------|-------|
| `Section_Connection.Text` | 연결 설정 | Connection |
| `Section_Communication.Text` | 통신 설정 | Communication |
| `Section_App.Text` | 앱 설정 | App Settings |
| `XBee_Header.Header` | XBee DigiMesh | XBee DigiMesh |
| `XBee_Desc.Text` | XBee DigiMesh 무선 연결 (STM32) | XBee DigiMesh wireless connection for STM32 |
| `XBeePort_Header.Header` | XBee 포트 | XBee Port |
| `ResponseTimeout_Header.Header` | 응답 타임아웃 | Response Timeout |
| `ResponseTimeout_Desc.Text` | 패킷 수신 후 다음 패킷을 기다리는 시간 (초) | Wait time between packets (seconds) |
| `PingSettings_Header.Header` | PING 설정 | PING Settings |
| `PingSettings_Desc.Text` | 주기 상태 확인과 PING 시간 페이로드를 설정합니다 | Configure periodic status check and PING time payload |
| `PeriodicPing_Header.Header` | 주기 PING | Periodic PING |
| `PeriodicPing_Desc.Text` | 활성화 시 주기적으로 PING 패킷을 전송합니다 | When enabled, sends PING packets periodically |
| `PingInterval_Header.Header` | PING 주기 | PING Interval |
| `PingInterval_Desc.Text` | PING 전송 주기 (초, 1–60) | PING transmission interval in seconds (1–60) |
| `PingCountryCode_Header.Header` | 국가 코드 | Country Code |
| `PingCountryCode_Desc.Text` | 2자리 ISO 국가 코드 (예: KR) | 2-letter ISO country code (e.g. KR) |
| `PingTimeZone_Header.Header` | 시간대 | Time Zone |
| `PingPreview_Header.Header` | 페이로드 미리보기 | Payload Preview |

- [ ] **Step 1: ko-KR/Resources.resw 에 문자열 추가**

  파일 끝의 `</root>` 바로 앞에 아래 블록을 삽입한다 (`</data>` 마지막 항목 다음, `</root>` 앞):

  ```xml
  <data name="Section_Connection.Text" xml:space="preserve">
    <value>연결 설정</value>
  </data>
  <data name="Section_Communication.Text" xml:space="preserve">
    <value>통신 설정</value>
  </data>
  <data name="Section_App.Text" xml:space="preserve">
    <value>앱 설정</value>
  </data>
  <data name="XBee_Header.Header" xml:space="preserve">
    <value>XBee DigiMesh</value>
  </data>
  <data name="XBee_Desc.Text" xml:space="preserve">
    <value>XBee DigiMesh 무선 연결 (STM32)</value>
  </data>
  <data name="XBeePort_Header.Header" xml:space="preserve">
    <value>XBee 포트</value>
  </data>
  <data name="ResponseTimeout_Header.Header" xml:space="preserve">
    <value>응답 타임아웃</value>
  </data>
  <data name="ResponseTimeout_Desc.Text" xml:space="preserve">
    <value>패킷 수신 후 다음 패킷을 기다리는 시간 (초)</value>
  </data>
  <data name="PingSettings_Header.Header" xml:space="preserve">
    <value>PING 설정</value>
  </data>
  <data name="PingSettings_Desc.Text" xml:space="preserve">
    <value>주기 상태 확인과 PING 시간 페이로드를 설정합니다</value>
  </data>
  <data name="PeriodicPing_Header.Header" xml:space="preserve">
    <value>주기 PING</value>
  </data>
  <data name="PeriodicPing_Desc.Text" xml:space="preserve">
    <value>활성화 시 주기적으로 PING 패킷을 전송합니다</value>
  </data>
  <data name="PingInterval_Header.Header" xml:space="preserve">
    <value>PING 주기</value>
  </data>
  <data name="PingInterval_Desc.Text" xml:space="preserve">
    <value>PING 전송 주기 (초, 1–60)</value>
  </data>
  <data name="PingCountryCode_Header.Header" xml:space="preserve">
    <value>국가 코드</value>
  </data>
  <data name="PingCountryCode_Desc.Text" xml:space="preserve">
    <value>2자리 ISO 국가 코드 (예: KR)</value>
  </data>
  <data name="PingTimeZone_Header.Header" xml:space="preserve">
    <value>시간대</value>
  </data>
  <data name="PingPreview_Header.Header" xml:space="preserve">
    <value>페이로드 미리보기</value>
  </data>
  ```

- [ ] **Step 2: en-US/Resources.resw 에 문자열 추가**

  동일하게 `</root>` 앞에 삽입:

  ```xml
  <data name="Section_Connection.Text" xml:space="preserve">
    <value>Connection</value>
  </data>
  <data name="Section_Communication.Text" xml:space="preserve">
    <value>Communication</value>
  </data>
  <data name="Section_App.Text" xml:space="preserve">
    <value>App Settings</value>
  </data>
  <data name="XBee_Header.Header" xml:space="preserve">
    <value>XBee DigiMesh</value>
  </data>
  <data name="XBee_Desc.Text" xml:space="preserve">
    <value>XBee DigiMesh wireless connection for STM32</value>
  </data>
  <data name="XBeePort_Header.Header" xml:space="preserve">
    <value>XBee Port</value>
  </data>
  <data name="ResponseTimeout_Header.Header" xml:space="preserve">
    <value>Response Timeout</value>
  </data>
  <data name="ResponseTimeout_Desc.Text" xml:space="preserve">
    <value>Wait time between packets (seconds)</value>
  </data>
  <data name="PingSettings_Header.Header" xml:space="preserve">
    <value>PING Settings</value>
  </data>
  <data name="PingSettings_Desc.Text" xml:space="preserve">
    <value>Configure periodic status check and PING time payload</value>
  </data>
  <data name="PeriodicPing_Header.Header" xml:space="preserve">
    <value>Periodic PING</value>
  </data>
  <data name="PeriodicPing_Desc.Text" xml:space="preserve">
    <value>When enabled, sends PING packets periodically</value>
  </data>
  <data name="PingInterval_Header.Header" xml:space="preserve">
    <value>PING Interval</value>
  </data>
  <data name="PingInterval_Desc.Text" xml:space="preserve">
    <value>PING transmission interval in seconds (1–60)</value>
  </data>
  <data name="PingCountryCode_Header.Header" xml:space="preserve">
    <value>Country Code</value>
  </data>
  <data name="PingCountryCode_Desc.Text" xml:space="preserve">
    <value>2-letter ISO country code (e.g. KR)</value>
  </data>
  <data name="PingTimeZone_Header.Header" xml:space="preserve">
    <value>Time Zone</value>
  </data>
  <data name="PingPreview_Header.Header" xml:space="preserve">
    <value>Payload Preview</value>
  </data>
  ```

- [ ] **Step 3: 커밋**

  ```bash
  git add AnimatronicsControlCenter/AnimatronicsControlCenter/Strings/ko-KR/Resources.resw
  git add AnimatronicsControlCenter/AnimatronicsControlCenter/Strings/en-US/Resources.resw
  git commit -m "feat: 설정 페이지 재구성용 다국어 문자열 추가"
  ```

---

## Task 2: SettingsPage.xaml 전면 재구성

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/SettingsPage.xaml`

변경 목표:
1. 3개 섹션 헤더 추가 (`BodyStrongTextBlockStyle`)
2. 가상 모드 카드를 COM 연결 Expander 안으로 이동
3. PING 설정을 `SettingsCard` → `SettingsExpander` + 하위 `SettingsCard` 5개로 분리
4. XBee 블록의 하드코딩 문자열 → 로컬라이제이션 키
5. 응답 타임아웃, PING, 언어, 시리얼 모니터 순서 정렬

- [ ] **Step 1: SettingsPage.xaml 전체를 아래 내용으로 교체**

  ```xml
  <Page
      x:Class="AnimatronicsControlCenter.UI.Views.SettingsPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
      xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
      xmlns:tk="using:CommunityToolkit.WinUI.Controls"
      xmlns:ui="using:CommunityToolkit.WinUI.UI"
      xmlns:cv="using:AnimatronicsControlCenter.UI.Converters"
      mc:Ignorable="d">

      <ScrollViewer>
          <StackPanel Spacing="8" Padding="20" MaxWidth="800" HorizontalAlignment="Left">
              <TextBlock Text="{x:Bind ViewModel.Strings.Get('Settings_Title.Text', ViewModel.Strings.Code), Mode=OneWay}"
                         Style="{StaticResource TitleTextBlockStyle}" Margin="0,0,0,16"/>

              <!-- ══════════════════════════════════════════
                   섹션 1: 연결 설정
                   ══════════════════════════════════════════ -->
              <TextBlock Text="{x:Bind ViewModel.Strings.Get('Section_Connection.Text', ViewModel.Strings.Code), Mode=OneWay}"
                         Style="{StaticResource BodyStrongTextBlockStyle}" Margin="0,4,0,4"/>

              <!-- COM 시리얼 연결 -->
              <tk:SettingsExpander Header="{x:Bind ViewModel.Strings.Get('Connection_Header.Header', ViewModel.Strings.Code), Mode=OneWay}"
                                   IsExpanded="True">
                  <tk:SettingsExpander.HeaderIcon>
                      <FontIcon Glyph="&#xE701;"/>
                  </tk:SettingsExpander.HeaderIcon>

                  <tk:SettingsExpander.Items>
                      <!-- COM Port -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('Port_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                          <StackPanel Orientation="Horizontal" Spacing="8">
                              <ComboBox ItemsSource="{x:Bind ViewModel.AvailablePorts}"
                                        SelectedItem="{x:Bind ViewModel.SelectedPort, Mode=TwoWay}"
                                        MinWidth="120"/>
                              <Button Command="{x:Bind ViewModel.RefreshPortsCommand}"
                                      Style="{StaticResource DateTimePickerFlyoutButtonStyle}">
                                  <ToolTipService.ToolTip>
                                      <ToolTip Content="{x:Bind ViewModel.Strings.Get('Refresh_Tooltip.Content', ViewModel.Strings.Code), Mode=OneWay}"/>
                                  </ToolTipService.ToolTip>
                                  <FontIcon Glyph="&#xE72C;" FontSize="14"/>
                              </Button>
                          </StackPanel>
                      </tk:SettingsCard>

                      <!-- Baud Rate -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('BaudRate_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                          <ComboBox ItemsSource="{x:Bind ViewModel.AvailableBaudRates}"
                                    SelectedItem="{x:Bind ViewModel.BaudRate, Mode=TwoWay}"
                                    MinWidth="120"/>
                      </tk:SettingsCard>

                      <!-- Virtual Mode (연결 그룹 안으로 이동) -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('VirtualMode_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                          <tk:SettingsCard.Description>
                              <TextBlock Text="{x:Bind ViewModel.Strings.Get('VirtualMode_Desc.Text', ViewModel.Strings.Code), Mode=OneWay}"
                                         Style="{StaticResource CaptionTextBlockStyle}"/>
                          </tk:SettingsCard.Description>
                          <ToggleSwitch IsOn="{x:Bind ViewModel.IsVirtualModeEnabled, Mode=TwoWay}"/>
                      </tk:SettingsCard>

                      <!-- Connect / Disconnect -->
                      <tk:SettingsCard IsClickEnabled="False">
                          <StackPanel Orientation="Horizontal" Spacing="12" HorizontalAlignment="Right">
                              <Border Background="{ThemeResource SystemControlBackgroundChromeMediumLowBrush}"
                                      CornerRadius="4" Padding="8,4" VerticalAlignment="Center">
                                  <StackPanel Orientation="Horizontal" Spacing="6">
                                      <FontIcon Glyph="{x:Bind ViewModel.IsConnectionActive, Mode=OneWay, Converter={StaticResource ConnectionIconConverter}}"
                                                FontSize="12"
                                                Foreground="{x:Bind ViewModel.IsConnectionActive, Mode=OneWay, Converter={StaticResource ConnectionColorConverter}}"/>
                                      <TextBlock Text="{x:Bind ViewModel.ConnectionStatusText, Mode=OneWay}"
                                                 Style="{StaticResource CaptionTextBlockStyle}"/>
                                  </StackPanel>
                              </Border>
                              <Button Content="{x:Bind ViewModel.ConnectButtonText, Mode=OneWay}"
                                      Command="{x:Bind ViewModel.ConnectCommand}"
                                      Style="{StaticResource AccentButtonStyle}"
                                      MinWidth="100"/>
                          </StackPanel>
                      </tk:SettingsCard>
                  </tk:SettingsExpander.Items>
              </tk:SettingsExpander>

              <!-- XBee DigiMesh -->
              <tk:SettingsExpander Header="{x:Bind ViewModel.Strings.Get('XBee_Header.Header', ViewModel.Strings.Code), Mode=OneWay}"
                                   IsExpanded="False">
                  <tk:SettingsExpander.HeaderIcon>
                      <FontIcon Glyph="&#xEC05;"/>
                  </tk:SettingsExpander.HeaderIcon>
                  <tk:SettingsExpander.Description>
                      <TextBlock Text="{x:Bind ViewModel.Strings.Get('XBee_Desc.Text', ViewModel.Strings.Code), Mode=OneWay}"
                                 Style="{StaticResource CaptionTextBlockStyle}"/>
                  </tk:SettingsExpander.Description>

                  <tk:SettingsExpander.Items>
                      <!-- XBee Port -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('XBeePort_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                          <StackPanel Orientation="Horizontal" Spacing="8">
                              <ComboBox ItemsSource="{x:Bind ViewModel.AvailablePorts}"
                                        SelectedItem="{x:Bind ViewModel.SelectedXBeePort, Mode=TwoWay}"
                                        MinWidth="120"/>
                              <Button Command="{x:Bind ViewModel.RefreshPortsCommand}"
                                      Style="{StaticResource DateTimePickerFlyoutButtonStyle}">
                                  <ToolTipService.ToolTip>
                                      <ToolTip Content="{x:Bind ViewModel.Strings.Get('Refresh_Tooltip.Content', ViewModel.Strings.Code), Mode=OneWay}"/>
                                  </ToolTipService.ToolTip>
                                  <FontIcon Glyph="&#xE72C;" FontSize="14"/>
                              </Button>
                          </StackPanel>
                      </tk:SettingsCard>

                      <!-- XBee Baud Rate -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('BaudRate_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                          <ComboBox ItemsSource="{x:Bind ViewModel.AvailableBaudRates}"
                                    SelectedItem="{x:Bind ViewModel.XBeeBaudRate, Mode=TwoWay}"
                                    MinWidth="120"/>
                      </tk:SettingsCard>

                      <!-- XBee Connect / Disconnect -->
                      <tk:SettingsCard IsClickEnabled="False">
                          <StackPanel Orientation="Horizontal" Spacing="12" HorizontalAlignment="Right">
                              <Border Background="{ThemeResource SystemControlBackgroundChromeMediumLowBrush}"
                                      CornerRadius="4" Padding="8,4" VerticalAlignment="Center">
                                  <StackPanel Orientation="Horizontal" Spacing="6">
                                      <FontIcon Glyph="{x:Bind ViewModel.IsXBeeConnected, Mode=OneWay, Converter={StaticResource ConnectionIconConverter}}"
                                                FontSize="12"
                                                Foreground="{x:Bind ViewModel.IsXBeeConnected, Mode=OneWay, Converter={StaticResource ConnectionColorConverter}}"/>
                                      <TextBlock Text="{x:Bind ViewModel.XBeeConnectionStatusText, Mode=OneWay}"
                                                 Style="{StaticResource CaptionTextBlockStyle}"/>
                                  </StackPanel>
                              </Border>
                              <Button Content="{x:Bind ViewModel.XBeeConnectButtonText, Mode=OneWay}"
                                      Command="{x:Bind ViewModel.XBeeConnectCommand}"
                                      Style="{StaticResource AccentButtonStyle}"
                                      MinWidth="100"/>
                          </StackPanel>
                      </tk:SettingsCard>
                  </tk:SettingsExpander.Items>
              </tk:SettingsExpander>

              <!-- ══════════════════════════════════════════
                   섹션 2: 통신 설정
                   ══════════════════════════════════════════ -->
              <TextBlock Text="{x:Bind ViewModel.Strings.Get('Section_Communication.Text', ViewModel.Strings.Code), Mode=OneWay}"
                         Style="{StaticResource BodyStrongTextBlockStyle}" Margin="0,16,0,4"/>

              <!-- 응답 타임아웃 -->
              <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('ResponseTimeout_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                  <tk:SettingsCard.HeaderIcon>
                      <FontIcon Glyph="&#xE916;"/>
                  </tk:SettingsCard.HeaderIcon>
                  <tk:SettingsCard.Description>
                      <TextBlock Text="{x:Bind ViewModel.Strings.Get('ResponseTimeout_Desc.Text', ViewModel.Strings.Code), Mode=OneWay}"
                                 Style="{StaticResource CaptionTextBlockStyle}"/>
                  </tk:SettingsCard.Description>
                  <NumberBox Value="{x:Bind ViewModel.ResponseTimeoutSeconds, Mode=TwoWay}"
                             Minimum="0.1" Maximum="60"
                             SpinButtonPlacementMode="Inline"
                             SmallChange="0.1" LargeChange="1"
                             MinWidth="120"/>
              </tk:SettingsCard>

              <!-- PING 설정 (SettingsCard → SettingsExpander) -->
              <tk:SettingsExpander Header="{x:Bind ViewModel.Strings.Get('PingSettings_Header.Header', ViewModel.Strings.Code), Mode=OneWay}"
                                   IsExpanded="False">
                  <tk:SettingsExpander.HeaderIcon>
                      <FontIcon Glyph="&#xE823;"/>
                  </tk:SettingsExpander.HeaderIcon>
                  <tk:SettingsExpander.Description>
                      <TextBlock Text="{x:Bind ViewModel.Strings.Get('PingSettings_Desc.Text', ViewModel.Strings.Code), Mode=OneWay}"
                                 Style="{StaticResource CaptionTextBlockStyle}"/>
                  </tk:SettingsExpander.Description>

                  <tk:SettingsExpander.Items>
                      <!-- 주기 PING 토글 -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('PeriodicPing_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                          <tk:SettingsCard.Description>
                              <TextBlock Text="{x:Bind ViewModel.Strings.Get('PeriodicPing_Desc.Text', ViewModel.Strings.Code), Mode=OneWay}"
                                         Style="{StaticResource CaptionTextBlockStyle}"/>
                          </tk:SettingsCard.Description>
                          <ToggleSwitch IsOn="{x:Bind ViewModel.IsPeriodicPingEnabled, Mode=TwoWay}"
                                        OnContent="" OffContent=""/>
                      </tk:SettingsCard>

                      <!-- PING 주기 -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('PingInterval_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                          <tk:SettingsCard.Description>
                              <TextBlock Text="{x:Bind ViewModel.Strings.Get('PingInterval_Desc.Text', ViewModel.Strings.Code), Mode=OneWay}"
                                         Style="{StaticResource CaptionTextBlockStyle}"/>
                          </tk:SettingsCard.Description>
                          <NumberBox Value="{x:Bind ViewModel.PingIntervalSeconds, Mode=TwoWay}"
                                     Minimum="1" Maximum="60"
                                     SpinButtonPlacementMode="Inline"
                                     SmallChange="1" LargeChange="5"
                                     MinWidth="120"/>
                      </tk:SettingsCard>

                      <!-- 국가 코드 -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('PingCountryCode_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                          <tk:SettingsCard.Description>
                              <TextBlock Text="{x:Bind ViewModel.Strings.Get('PingCountryCode_Desc.Text', ViewModel.Strings.Code), Mode=OneWay}"
                                         Style="{StaticResource CaptionTextBlockStyle}"/>
                          </tk:SettingsCard.Description>
                          <TextBox Text="{x:Bind ViewModel.PingCountryCode, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                   MaxLength="2" MinWidth="100"/>
                      </tk:SettingsCard>

                      <!-- 시간대 -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('PingTimeZone_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                          <ComboBox ItemsSource="{x:Bind ViewModel.PingTimeZoneOptions}"
                                    SelectedItem="{x:Bind ViewModel.SelectedPingTimeZoneOption, Mode=TwoWay}"
                                    DisplayMemberPath="Label"
                                    MinWidth="280"/>
                      </tk:SettingsCard>

                      <!-- 페이로드 미리보기 -->
                      <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('PingPreview_Header.Header', ViewModel.Strings.Code), Mode=OneWay}"
                                       IsClickEnabled="False">
                          <StackPanel Spacing="4" MinWidth="280">
                              <TextBlock Text="{x:Bind ViewModel.PingPreviewText, Mode=OneWay}"
                                         Style="{StaticResource CaptionTextBlockStyle}"
                                         TextWrapping="Wrap"/>
                              <TextBlock Text="{x:Bind ViewModel.PingPayloadPreviewText, Mode=OneWay}"
                                         FontFamily="Consolas"
                                         Style="{StaticResource CaptionTextBlockStyle}"
                                         TextWrapping="Wrap"/>
                          </StackPanel>
                      </tk:SettingsCard>
                  </tk:SettingsExpander.Items>
              </tk:SettingsExpander>

              <!-- ══════════════════════════════════════════
                   섹션 3: 앱 설정
                   ══════════════════════════════════════════ -->
              <TextBlock Text="{x:Bind ViewModel.Strings.Get('Section_App.Text', ViewModel.Strings.Code), Mode=OneWay}"
                         Style="{StaticResource BodyStrongTextBlockStyle}" Margin="0,16,0,4"/>

              <!-- 언어 -->
              <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('Language_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                  <tk:SettingsCard.HeaderIcon>
                      <FontIcon Glyph="&#xE774;"/>
                  </tk:SettingsCard.HeaderIcon>
                  <ComboBox ItemsSource="{x:Bind ViewModel.Languages}"
                            SelectedItem="{x:Bind ViewModel.SelectedLanguage, Mode=TwoWay}"
                            DisplayMemberPath="Name"
                            MinWidth="150"/>
              </tk:SettingsCard>

              <!-- 시리얼 모니터 -->
              <tk:SettingsCard Header="{x:Bind ViewModel.Strings.Get('SerialMonitor_Header.Header', ViewModel.Strings.Code), Mode=OneWay}">
                  <tk:SettingsCard.HeaderIcon>
                      <FontIcon Glyph="&#xE8D2;"/>
                  </tk:SettingsCard.HeaderIcon>
                  <tk:SettingsCard.Description>
                      <TextBlock Text="{x:Bind ViewModel.Strings.Get('SerialMonitor_Desc.Text', ViewModel.Strings.Code), Mode=OneWay}"
                                 Style="{StaticResource CaptionTextBlockStyle}"/>
                  </tk:SettingsCard.Description>
                  <Button Content="{x:Bind ViewModel.Strings.Get('SerialMonitor_OpenButton', ViewModel.Strings.Code), Mode=OneWay}"
                          Command="{x:Bind ViewModel.OpenSerialMonitorCommand}"
                          Style="{StaticResource AccentButtonStyle}"/>
              </tk:SettingsCard>

          </StackPanel>
      </ScrollViewer>

      <Page.Resources>
          <d:BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>
          <cv:BoolToIconGlyphConverter x:Key="ConnectionIconConverter"/>
          <cv:BoolToColorBrushConverter x:Key="ConnectionColorConverter"/>
      </Page.Resources>
  </Page>
  ```

- [ ] **Step 2: 빌드 확인**

  Visual Studio 또는 터미널에서:
  ```
  dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj
  ```
  Expected: 빌드 오류 없음. XAML 파싱 오류 없음.

- [ ] **Step 3: 앱 실행 후 수동 확인 체크리스트**

  - [ ] 설정 페이지 진입 시 "연결 설정 / 통신 설정 / 앱 설정" 3개 섹션 헤더 보임
  - [ ] COM 연결 Expander 내에 가상 모드 토글이 포함됨 (독립 카드 아님)
  - [ ] XBee 블록 헤더/설명이 현재 언어로 표시됨
  - [ ] 응답 타임아웃이 "통신 설정" 섹션에 위치함
  - [ ] PING 설정이 Expander 형태로 접힘/펼침 가능
  - [ ] PING Expander 내 5개 하위 카드가 각각 독립 행으로 표시됨
  - [ ] 언어를 en-US로 전환 시 모든 텍스트가 영어로 바뀜
  - [ ] 시리얼 모니터 열기 버튼 정상 동작

- [ ] **Step 4: 커밋**

  ```bash
  git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/SettingsPage.xaml
  git commit -m "refactor: 설정 페이지 3섹션 구조로 재구성, PING Expander 분리, 가상 모드 연결 그룹 이동"
  ```

---

## 변경 전후 요약

| 항목 | 변경 전 | 변경 후 |
|------|---------|---------|
| 구조 | 평면 목록 7개 항목 | 3섹션 + Expander 계층 |
| 가상 모드 | 독립 SettingsCard | COM 연결 Expander 내부 |
| PING 설정 | 단일 SettingsCard (StackPanel 5개 중첩) | SettingsExpander + SettingsCard 5개 |
| 하드코딩 문자열 | 6개 항목 한국어 하드코딩 | 전체 다국어 리소스 사용 |
| 응답 타임아웃 | 연결 그룹과 혼재 | 통신 설정 섹션으로 이동 |

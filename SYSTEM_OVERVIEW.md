# 시스템 구조 및 화면 구성요소 설명

이 문서는 Animatronics Control Center의 장치 모델, 화면 구성요소, 그리고 가상 장치 시뮬레이션 구조를 설명합니다.

## 1. 장치 모델 (Device Model)

`Core/Models/Device.cs`에 정의된 장치 모델은 애니마트로닉스 장치의 상태와 제어 정보를 담고 있습니다.

### 주요 속성 (Properties)
- **Id**: 장치 고유 식별자 (Integer)
- **IsConnected**: 연결 상태 (Boolean)
- **StatusMessage**: 현재 상태 메시지 (String)
- **MotionState**: 동작 상태 (Enum: Idle, Playing, Paused, Stopped)
- **MotionTotalTime**: 모션 총 재생 시간
- **MotionDataCount**: 모션 데이터 개수
- **Motors**: 장치에 포함된 모터 목록 (`ObservableCollection<MotorState>`)

### 모터 상태 (MotorState)
각 모터는 다음 정보를 가집니다:
- **Id**: 모터 ID
- **GroupId / SubId**: 모터 그룹 및 서브 ID
- **Position**: 현재 위치 값
- **Type**: 모터 타입 (예: Servo, DC, Stepper)
- **Status**: 모터 상태 (예: Normal, Error)

---

## 2. 화면 구성요소 (UI Components)

UI는 MVVM (Model-View-ViewModel) 패턴을 따르며 `UI/Views`와 `UI/ViewModels`로 구성됩니다.

### 2.1 대시보드 (Dashboard)
- **View**: `DashboardPage.xaml`
- **ViewModel**: `DashboardViewModel.cs`
- **기능**:
  - 연결된 장치 목록 표시 (`Devices`)
  - 장치 스캔 기능 (`ScanAsync`)
  - 스캔 다이얼로그(`ScanDialog`)를 통해 새로운 장치 검색

### 2.2 장치 상세 (Device Detail)
- **View**: `DeviceDetailPage.xaml`
- **ViewModel**: `DeviceDetailViewModel.cs`
- **기능**:
  - **장치 제어**:
    - 모션 제어: 재생(`Play`), 정지(`Stop`), 일시정지(`Pause`)
    - 개별 모터 제어: 모터 위치 이동 (`MoveMotorAsync`)
  - **파일 관리 (File System)**:
    - 장치 내 파일 목록 조회 (`RefreshFilesAsync`)
    - 파일 내용 읽기 및 편집 (`LoadFileContentAsync`)
    - 파일 저장 (`SaveFileAsync`)
    - 파일 무결성 검증 (`VerifyFileAsync`): 로컬 편집 내용과 장치 저장 내용 비교

---

## 3. 가상 장치 및 시뮬레이션 (Virtual Device Manager)

`Infrastructure/VirtualDeviceManager.cs`는 실제 하드웨어 없이 개발 및 테스트를 위해 장치 동작을 시뮬레이션합니다.

### 주요 기능
- **가상 파일 시스템**: 각 장치 ID별로 독립적인 가상 파일 시스템을 메모리에 유지합니다.
  - 기본 디렉토리 구조: `Error`, `Log`, `Media`, `Midi`, `Setting`
- **명령어 처리 (Command Processing)**:
  - `ping`: 연결 확인
  - `move`: 모터 이동 시뮬레이션
  - `motion_ctrl`: 모션 제어 (play/stop/pause)
  - `get_files` / `get_file`: 파일 목록 및 내용 조회
  - `save_file`: 가상 파일 시스템에 내용 저장
  - `verify_file`: 전송된 내용과 저장된 내용 비교

이 구조를 통해 하드웨어 없이도 UI 및 로직 개발이 가능하며, 실제 장치 연동 시 `ISerialService` 구현체만 교체하거나 수정하여 대응할 수 있도록 설계되어 있습니다.


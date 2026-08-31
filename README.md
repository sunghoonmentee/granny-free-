# granny-free-

Unity 6로 만드는 1인칭 스텔스 호러 탈출 게임.

낡은 저택에 갇힌 플레이어가 5일 안에 탈출해야 한다. 집주인은 **소리로 사냥한다** —
발소리, 쾅 닫힌 문, 떨어뜨린 물건 하나가 곧바로 추격으로 이어진다.

> 이 프로젝트는 *Granny*(DVloper, 2017)의 게임 구조에서 영감을 받은 **오리지널 작품**입니다.
> 원작의 모델·텍스처·사운드·상표는 일절 사용하지 않습니다. 모든 에셋은 자체 제작이거나
> CC0/무료 라이선스이며 아래 [크레딧](#크레딧)에 출처를 명시합니다.

---

## 게임 개요

| | |
|---|---|
| 장르 | 1인칭 스텔스 호러 / 탈출 |
| 엔진 | Unity 6000.5.8f1 (URP) |
| 플랫폼 | Windows 64-bit |
| 조작 | 키보드 + 마우스 |

### 핵심 루프

1. **탐색** — 지하실·1층·2층·다락을 뒤져 도구를 찾는다. 아이템 위치는 매 플레이 랜덤.
2. **회피** — 소리를 내면 그녀가 온다. 앉아서 걷고, 옷장이나 침대 밑에 숨는다.
3. **탈출** — 정문의 3중 잠금(판자 · 경보선 · 자물쇠)을 각각 알맞은 도구로 해제한다.
4. **5일** — 잡히면 기절해 침대에서 다음 날 아침에 깨어난다. 6일째는 없다.

---

## 개발 상태

| 단계 | 내용 | 상태 |
|---|---|---|
| P1 | 저장소·Unity 프로젝트 셋업 (URP / Input System / AI Navigation) | ✅ 완료 |
| P2 | 플레이어 컨트롤러 (이동·달리기·앉기·스태미나·손전등) | 🚧 진행 중 |
| P3 | 상호작용 · 아이템 · 인벤토리 · 물리 문 | ⬜ |
| P4 | 소음 전파 시스템 | ⬜ |
| P5 | 그래니 AI (NavMesh · 시야 · 청각 · FSM) | ⬜ |
| P6 | 저택 레벨 (4개 층) · 조명 · 랜덤 스폰 | ⬜ |
| P7 | 목표 · 정문 3중 잠금 · 보조 탈출 루트 | ⬜ |
| P8 | 5일 루프 · 세이브 · 난이도 · 메뉴 | ⬜ |
| P9 | 아트/오디오 폴리시 | ⬜ |
| P10 | 빌드 · 배포 | ⬜ |

---

## 개발 환경 설정

```bash
git clone https://github.com/sunghoonmentee/granny-free-.git
cd granny-free-
git lfs install
git lfs pull
```

Unity Hub에서 **6000.5.8f1** 버전으로 `GrannyGame/` 폴더를 엽니다.

### 명령줄 작업 — `tools/unity.ps1`

에디터를 열지 않고 컴파일·셋업·테스트를 돌립니다. 로그는 `.unity-logs/`에 남습니다.

```powershell
./tools/unity.ps1 compile
```

```powershell
./tools/unity.ps1 test -Platform EditMode
```

```powershell
./tools/unity.ps1 test -Platform PlayMode
```

프로젝트 셋업을 다시 적용해야 할 때 (URP·레이어·씬 재생성):

```powershell
./tools/unity.ps1 run -Method Granny.EditorTools.ProjectBootstrap.Run
```

플레이어 프리팹을 다시 만들 때:

```powershell
./tools/unity.ps1 run -Method Granny.EditorTools.PlayerRigBuilder.Run
```

결과를 다시 읽어보려면:

```powershell
./tools/report-tests.ps1 unity-logs/TestResults-PlayMode.xml
```

> 배치모드 Unity에는 두 가지 함정이 있어 `unity.ps1`이 이를 흡수합니다.
> 스크립트를 새로 컴파일하면 요청한 작업을 건너뛰고 종료하므로 한 번 자동 재시도하고,
> 비정상 종료 시 남는 잠금 파일도 정리합니다.
> (로그 폴더 이름에 점을 앞에 붙이면 Unity가 경로를 거부하므로 `unity-logs/`입니다.)

---

## 프로젝트 구조

```
GrannyGame/Assets/_Project/
├─ Scenes/        Boot · MainMenu · House
├─ Settings/      URP 파이프라인 에셋 · 볼륨 프로파일
├─ Input/         GrannyControls.inputactions
├─ Data/          ItemDefinition · DifficultyProfile (ScriptableObject)
├─ Prefabs/       Player · Granny · Items · Props · UI
├─ Editor/        ProjectBootstrap (헤드리스 셋업) · BuildScript
├─ Tests/         EditMode · PlayMode
└─ Scripts/
   ├─ Core/         GameManager · DayCycle · SaveSystem
   ├─ Player/       Motor · Look · Interactor · Inventory
   ├─ AI/           GrannyBrain(FSM) · Perception · BearTrap
   ├─ Interaction/  IInteractable · PickupItem · HingeDoor · HidingSpot
   ├─ Audio/        NoiseManager · NoiseEmitter
   └─ UI/           HUD · ItemBar · PauseMenu
```

---

## 조작법

| 키 | 게임패드 | 동작 |
|---|---|---|
| `W A S D` / 방향키 | 왼쪽 스틱 | 이동 — 2.6 m/s |
| `Shift` | L3 | 달리기 — 4.8 m/s, 스태미나 6초, 소음 큼 |
| `Ctrl` / `C` | B | 앉기 — 1.15 m/s, 거의 무음 |
| 마우스 | 오른쪽 스틱 | 시점 |
| `E` | X | 상호작용 / 줍기 |
| `Q` | — | 손에 든 물건 놓기 |
| 마우스 좌클릭 | RT | 던지기 |
| `F` | D-Pad ↑ | 손전등 (배터리 240초) |
| `1`–`5` | — | 아이템 슬롯 |
| `Esc` | Start | 일시정지 |

스태미나를 전부 소진하면 35%까지 회복될 때까지 달릴 수 없습니다.
손전등 배터리가 15% 아래로 떨어지면 깜빡이기 시작합니다.

---

## 크레딧

에셋을 도입할 때마다 출처와 라이선스를 여기에 추가합니다.

| 에셋 | 출처 | 라이선스 |
|---|---|---|
| — | — | — |

---

## 라이선스

코드: MIT. 에셋은 위 크레딧 표의 개별 라이선스를 따릅니다.

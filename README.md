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
| P1 | 저장소·Unity 프로젝트 셋업 (URP / Input System / AI Navigation) | 🚧 진행 중 |
| P2 | 플레이어 컨트롤러 (이동·달리기·앉기·스태미나·손전등) | ⬜ |
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

### 명령줄에서 컴파일 확인

```bash
"/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe" -batchmode -quit -nographics -projectPath GrannyGame -logFile -
```

### 테스트 실행

```bash
"/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe" -batchmode -runTests -projectPath GrannyGame -testPlatform EditMode -testResults TestResults-EditMode.xml
```

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

| 키 | 동작 |
|---|---|
| `W A S D` | 이동 |
| `Shift` | 달리기 (스태미나 소모, 소음 큼) |
| `Ctrl` | 앉기 (느리지만 거의 무음) |
| `E` | 상호작용 / 줍기 |
| `Q` | 손에 든 물건 놓기 |
| `마우스 좌클릭` | 던지기 |
| `F` | 손전등 |
| `1`–`5` | 아이템 슬롯 |
| `Esc` | 일시정지 |

---

## 크레딧

에셋을 도입할 때마다 출처와 라이선스를 여기에 추가합니다.

| 에셋 | 출처 | 라이선스 |
|---|---|---|
| — | — | — |

---

## 라이선스

코드: MIT. 에셋은 위 크레딧 표의 개별 라이선스를 따릅니다.

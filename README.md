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
2. **회피** — 소리를 내면 그녀가 온다. 앉아서 걷고, 옷장에 숨는다.
3. **탈출** — 정문의 3중 잠금(판자 · 경보선 · 자물쇠)을 각각 알맞은 도구로 해제한다.
4. **5일** — 잡히면 기절해 침대에서 다음 날 아침에 깨어난다. 6일째는 없다.

### 저택 구조

24 × 20 m 부지에 4개 층. 층고 3.2 m.

| 층 | 높이 | 구성 |
|---|---|---|
| 다락 | +6.4 m | 단일 공간 (16 × 14 m) |
| 2층 | +3.2 m | 침실 3 + 복도. **플레이어는 남동쪽 침실에서 깨어남** |
| 1층 | 0 m | 현관홀 · 거실 · 주방. **정문은 북쪽 벽** |
| 지하실 | −3.2 m | 창고 · 보일러실. **그래니 시작 위치** |

계단 3개는 일부러 서로 다른 구석에 있습니다. 다락에서 지하실까지 가려면 집을 두 번 가로질러야 하고,
그래서 그녀가 지금 어디 있는지가 반응해야 할 정보가 아니라 계속 추적해야 할 정보가 됩니다.

서랍은 6개, 필요한 도구는 3개입니다. 절반은 비어 있습니다 — 뒤지는 행위에 비용이 없으면 찾는 것에도 의미가 없습니다.

### 세이브

기록은 **하루가 시작될 때만** 저장됩니다. 원할 때 저장할 수 있으면 모든 실수를 되돌릴 수 있고,
5일이라는 제한은 그 실수를 위해 존재합니다.

이어하기로 돌아오는 것은 며칠째인지, 정문에서 뜯어낸 잠금장치, 이미 뒤진 서랍입니다.
누가 어디 서 있었는지는 복원되지 않습니다 — 추격 중에 종료하는 것이 탈출 수단이 되면 안 되니까요.

---

## 개발 상태

| 단계 | 내용 | 상태 |
|---|---|---|
| P1 | 저장소·Unity 프로젝트 셋업 (URP / Input System / AI Navigation) | ✅ 완료 |
| P2 | 플레이어 컨트롤러 (이동·달리기·앉기·스태미나·손전등) | ✅ 완료 |
| P3 | 상호작용 · 아이템 · 인벤토리 · 문/서랍/옷장 · HUD | ✅ 완료 |
| P4 | 그래니 AI · 소음 청취 · 5일 루프 · 정문 3중 잠금 | ✅ 완료 |
| P5 | 저택 레벨 (4개 층) · 조명 · 랜덤 스폰 | ✅ 완료 |
| P6 | 세이브/로드 · 메인 메뉴 · 일시정지 · 설정 | 🚧 진행 중 |
| P7 | 보조 탈출 루트 (차고 · 하수구) · 금고 퍼즐 | ⬜ |
| P8 | 곰덫 배치 · 밸런싱 | ⬜ |
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

아이템·가구·HUD를 다시 만들고 House 씬에 배치할 때:

```powershell
./tools/unity.ps1 run -Method Granny.EditorTools.ContentBuilder.Run
```

저택(4개 층·계단·가구·조명·NavMesh)을 다시 만들 때:

```powershell
./tools/unity.ps1 run -Method Granny.EditorTools.HouseBuilder.Run
```

그래니 프리팹·난이도 테이블을 다시 만들 때:

```powershell
./tools/unity.ps1 run -Method Granny.EditorTools.GrannyBuilder.Run
```

정문(3중 잠금)을 다시 만들 때:

```powershell
./tools/unity.ps1 run -Method Granny.EditorTools.EscapeBuilder.Run
```

> 씬을 처음부터 다시 만들 경우 **순서가 중요합니다**:
> `ProjectBootstrap` → `ContentBuilder` → `PlayerRigBuilder` → `HouseBuilder` → `GrannyBuilder` → `EscapeBuilder` → `MenuBuilder`.
> 각 빌더는 앞선 빌더가 만든 프리팹과 마커를 참조하고, 마지막에 NavMesh를 다시 굽습니다.
> `ContentBuilder`가 `HouseBuilder`보다 먼저여야 합니다 — 집이 문·서랍·옷장 프리팹을 가져다 쓰기 때문입니다.

### 실제 플레이 장면 녹화

지하의 아가타가 2층에서 깨진 병 소리를 듣고 올라오는지를 실제로 실행해 프레임으로 남깁니다.
결과는 `unity-logs/scenario/<시각>/`에 프레임 이미지와 이동 경로 CSV로 저장됩니다.

```powershell
./tools/unity.ps1 test -Platform PlayMode -WithGraphics -Extra @('-runScenario','-testFilter','HearingScenarioCapture')
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

## 난이도

난이도는 데미지 수치가 아니라 **그녀가 한 번에 얼마나 많은 집을 감지하는가**입니다.
청각 반경 · 시야각 · 반응 지연 · 기억 시간 · 수색 끈기가 함께 움직입니다.

| 난이도 | 일수 | 추격 속도 | 청각 배율 | 반응 지연 | 시야 | 옷장 수색 확률 |
|---|---|---|---|---|---|---|
| Practice | ∞ | 2.6 m/s | ×0.5 | 1.20초 | 8 m / 70° | 15% |
| Easy | 5 | 3.4 m/s | ×0.75 | 0.70초 | 10 m / 85° | 30% |
| Normal | 5 | 4.1 m/s | ×1.0 | 0.35초 | 13 m / 105° | 50% |
| Hard | 5 | 4.7 m/s | ×1.4 | 0.15초 | 17 m / 125° | 70% |
| Extreme | 5 | 5.4 m/s | ×1.9 | 0.05초 | 22 m / 150° | 90% |

반응 지연이 플레이어의 유일한 선수(先手)입니다. Extreme에서는 소리를 낸 순간 이미 늦습니다.

---

## 크레딧

에셋을 도입할 때마다 출처와 라이선스를 여기에 추가합니다.

| 에셋 | 출처 | 라이선스 |
|---|---|---|
| — | — | — |

---

## 라이선스

코드: MIT. 에셋은 위 크레딧 표의 개별 라이선스를 따릅니다.

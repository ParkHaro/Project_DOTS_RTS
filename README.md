# Unity DOTS RTS

> Unity DOTS(Data-Oriented Technology Stack) 기반의 실시간 전략(RTS) 게임 포트폴리오 프로젝트

대규모 유닛을 고성능으로 처리하기 위해 전통적인 MonoBehaviour 대신 **ECS(Entity Component System) 아키텍처**를 채택했습니다. 모든 게임 시스템은 **Burst Compiler**로 컴파일되며, **IJobEntity** 기반 병렬 처리를 통해 멀티코어 CPU 활용을 극대화합니다.

<!-- 스크린샷이나 GIF를 추가하면 프로젝트를 시각적으로 전달할 수 있습니다 -->
<!-- ![Gameplay](docs/gameplay.gif) -->

## 기술 스택

| 분류 | 기술 | 버전 |
|------|------|------|
| 엔진 | Unity 6 | 6000.3.10f1 |
| 렌더링 | Universal Render Pipeline (URP) | 17.3.0 |
| ECS | Unity Entities | 1.4.4 |
| 물리 | Unity Physics | 1.4.4 |
| 렌더링 통합 | Entities Graphics | 1.4.17 |
| 카메라 | Cinemachine | 3.1.5 |
| 컴파일러 | Burst Compiler | 1.8.28 |

## 핵심 기술 구현

### 1. FlowField 경로탐색

그리드 기반 FlowField 알고리즘을 구현하여 대규모 유닛의 경로탐색을 처리합니다.

- **다중 그리드맵**: 유닛별 독립적인 경로 요청 큐잉 시스템
- **레이캐스트 최적화**: 직선 경로 가능 시 FlowField를 건너뛰는 최적화
- **물리 기반 벽 감지**: Unity Physics `CollisionWorld`를 활용한 동적 장애물 감지
- **Job 병렬화**: 그리드 초기화 및 비용맵 업데이트를 Job으로 분리하여 병렬 처리

### 2. ECS 커스텀 애니메이션 시스템

Unity Animator를 사용하지 않고, ECS 환경에서 동작하는 메시 기반 애니메이션 시스템을 직접 구현했습니다.

- **ScriptableObject 기반 데이터**: 애니메이션 프레임 데이터를 SO로 관리하여 Baker에서 엔티티로 변환
- **상태 머신**: `AnimationStateSystem`이 유닛 상태(Idle/Walk/Aim/Shoot/Attack)에 따라 애니메이션 전환
- **Burst 호환**: 모든 애니메이션 로직이 Burst-compiled Job 내에서 실행

### 3. 전장의 안개 (Fog of War)

Shader Graph와 ECS를 조합한 시야 시스템입니다.

- **Shader Graph**: 시야 영역을 렌더링하는 커스텀 셰이더
- **Physics 기반 시야 판정**: `CollisionWorld.SphereCast`로 시야 차단 감지
- **지속성 시스템**: MonoBehaviour(`FogOfWarPersistent`)가 탐색한 영역을 텍스처에 기록하여 유지
- **타이머 최적화**: 매 프레임이 아닌 주기적 업데이트로 성능 확보
- **DisableRendering**: 시야 밖 엔티티의 렌더링을 ECS 컴포넌트로 직접 제어

### 4. 성능 최적화 전략

| 기법 | 적용 시스템 | 설명 |
|------|-------------|------|
| `IJobEntity` 병렬 처리 | FindTarget, FogOfWar, Animation 등 | `ScheduleParallel()`로 멀티코어 분산 |
| `ComponentLookup` 캐싱 | FindTarget, FogOfWar | `OnCreate`에서 Lookup 생성, `OnUpdate`에서 재사용 |
| 타이머 기반 주기적 업데이트 | FindTarget, FogOfWar | 매 프레임 물리 쿼리 대신 간격을 두고 실행 |
| `NativeList<T>(Allocator.TempJob)` | FindTarget | 임시 할당으로 GC 압박 최소화 |
| `EntityCommandBuffer` 병렬 쓰기 | FogOfWar, HealthDead 등 | `ParallelWriter`로 구조적 변경을 병렬 처리 |

## 게임 기능

### 유닛
- **선택**: 클릭/드래그 박스 선택, 유닛 타입별 필터링
- **이동**: FlowField 경로탐색, 원형 포메이션 자동 배치
- **유닛 타입**: Scout(정찰), Soldier(병사) — ScriptableObject로 스탯 정의

### 전투
- **근접 공격**: `MeleeAttackSystem` — 사거리 내 직접 데미지
- **원거리 공격**: `ShootAttackSystem` — 투사체 엔티티 생성, 탄도 계산
- **목표 탐색**: `FindTargetSystem` — `OverlapSphere`로 범위 내 적 감지, Faction 기반 피아식별
- **어그로**: 피격 시 `TargetOverride`로 공격자에게 자동 반격
- **사망**: 체력 0 시 래그돌 프리팹 생성 후 엔티티 제거

### 건물
- **배치**: 고스트 프리뷰 → 충돌 검사 → 건설 시작
- **건설**: 시간 경과에 따른 건설 애니메이션 (스케일 보간)
- **종류**: 병영(유닛 생산), 방어 타워(자동 공격), 채굴소(자원 수집), 본부(게임 오버 조건)

### 자원
- **3종 자원**: Gold, Iron, Oil
- **채굴**: 자원 노드 근처에 채굴 건물 배치 → 자동 수집
- **비용**: 건물 건설과 유닛 생산에 자원 소모

### AI
- **호드 스포너**: 주기적으로 좀비 웨이브 생성
- **적 행동**: 스폰된 적이 플레이어 본부를 향해 자동 공격

### UI
- 미니맵 (아군/적 유닛·건물 아이콘 구분 표시)
- 자원 현황 HUD
- 건물 배치 UI (건물 타입 선택 + 비용 표시)
- 병영 유닛 생산 큐 UI
- 메인 메뉴 / 게임 오버 화면

## 아키텍처

```
┌─────────────────────────────────────────────────┐
│              MonoBehaviour Layer                 │
│  (Input, Camera, UI, Fog of War Persistent)     │
└──────────────────────┬──────────────────────────┘
                       │ SystemAPI / EntityManager
┌──────────────────────▼──────────────────────────┐
│                  ECS World                       │
│                                                  │
│  Authoring (Baker)                               │
│    GameObject Inspector → Entity + Components    │
│                                                  │
│  Components (IComponentData)                     │
│    UnitMover, Health, Target, Faction,           │
│    FindTarget, BuildingBarracks, ...             │
│                                                  │
│  Systems (ISystem + [BurstCompile])              │
│    UnitMoverSystem, FindTargetSystem,            │
│    ShootAttackSystem, GridSystem, ...            │
│    → IJobEntity.ScheduleParallel()               │
│                                                  │
└──────────────────────┬──────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────┐
│          Entities Graphics + URP                 │
│     (Burst-compiled 렌더링 파이프라인)            │
└─────────────────────────────────────────────────┘
```

## 프로젝트 구조

```
Assets/
├── Scripts/
│   ├── Authoring/       # MonoBehaviour Baker (47개) — GO → Entity 변환
│   ├── Systems/         # ISystem (37개) — Burst-compiled 게임 로직
│   ├── MonoBehaviours/  # Unity API 브릿지 (12개) — Input, Camera, FogOfWar
│   └── UI/              # UI 컨트롤러 (7개) — uGUI 기반 인게임 UI
│
├── Prefabs/             # 유닛(4종), 건물(8종), 자원노드(3종), 이펙트
├── ScriptableObject/    # 유닛·건물·자원·애니메이션 설정 데이터
├── Scenes/
│   ├── GameScene/       # 메인 게임 씬 + ECS 서브씬
│   └── MainMenuScene/   # 메인 메뉴 씬 + 서브씬
├── Materials/           # 머티리얼, Shader Graph (FogOfWar, Minimap 등)
├── Meshes/              # 캐릭터·건물 3D 메시 및 애니메이션 데이터
└── Textures/            # 텍스처, UI 아이콘
```

## 실행 방법

1. [Unity Hub](https://unity.com/download)에서 Unity **6000.3.10f1** 버전을 설치합니다.
2. 저장소를 클론합니다.
   ```bash
   git clone https://github.com/<your-username>/Project_DOTS_RTS.git
   ```
3. Unity Hub에서 프로젝트를 열고 패키지 임포트가 완료될 때까지 기다립니다.
4. `Assets/Scenes/MainMenuScene.unity`를 열고 **Play** 버튼을 눌러 실행합니다.

### 조작법

| 입력 | 동작 |
|------|------|
| 좌클릭 | 유닛/건물 선택 |
| 좌클릭 드래그 | 범위 선택 |
| 우클릭 | 이동/공격 명령 |
| 마우스 휠 | 줌 인/아웃 |
| WASD / 화면 가장자리 | 카메라 이동 |

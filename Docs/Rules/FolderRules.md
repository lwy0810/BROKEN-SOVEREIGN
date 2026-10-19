# 폴더 구조 규칙

스크립트·에셋을 새로 만들거나 옮길 때 따르는 규칙입니다. 규칙은 이 문서에만 적습니다.

## Assets 폴더

- `Assets/`
	- `Scripts/` - C# 스크립트
		- `Core/` - 이벤트 버스, 공용 enum·유틸리티, 게임 설정(`GameSettings`: 지도 표시 방식 등, PlayerPrefs 저장)
			- `Events/` - 이벤트 구조체 (`~Event`)
		- `Player/` - 클릭 이동, 입력 처리, 쿼터뷰 카메라
		- `Combat/` - 체력, 피해 계산(순수 클래스), 상태이상
		- `Skill/` - 기술 정의·실행, 슬롯 레벨·강화 분기, 직업 자원(신앙 등)
		- `Ascension/` - 격 단계, 돌파 조건, 스탯 분배, 직업 정의
		- `Hero/` - 영웅의 길, 영웅 고유 메커니즘 규칙 객체
		- `Relic/` - 신물창, 신화 세트 효과
		- `Equipment/` - 무기 정의·장착(`PlayerWeapon`), 일반 장비창, 무기 계열 보너스
		- `Achievement/` - 업적, 시련
		- `Enemy/` - 적 AI, 보스, 격 수확 패턴
		- `Map/` - 지도 데이터(`LevelMap`: 지도 그림·월드 범위), 지도 표시 대상(`MapMarker`)
		- `Town/` - 마을 NPC(`TownNpc`), NPC 상호작용(`NpcInteractor`), NPC 기능(`INpcService` 구현. 예: `HealerService`)
		- `UI/` - HUD, 캐릭터·신물·장비·스킬·업적 창
		- `Manager/` - 게임 상태, 체크포인트
	- `Data/` - ScriptableObject 에셋
		- `Skills/` - 기술 정의, 강화 분기
		- `Heroes/` - 영웅의 길 정의
		- `Relics/` - 신물 정의, 신화 세트
		- `Equipment/` - 무기·일반 장비 정의. 예: `WeaponHolyMace.asset`
		- `Enemies/` - 적·보스 정의
		- `Ascension/` - 격 단계별 돌파 조건, 시련, 직업 정의
		- `Balance/` - 신앙·스탯·피해 등 밸런스 수치
	- `Tests/` - 테스트
		- `EditMode/` - 순수 로직 테스트
		- `PlayMode/` - 플레이 흐름 테스트
	- `Scenes/` - 씬
		- `Sandbox/` - 기능별 테스트 씬
		- `Levels/` - 게임 맵 (`World` 오픈 필드·`BossArena`, LevelTools 생성). 씬 이름 폴더에 NavMesh 데이터·강 물 메시·걷기 영역 메시
	- `Prefabs/` - 프리팹
		- `Characters/` - 캐릭터 모델 프리팹 (Animator·머티리얼 연결). 예: `PaladinModel.prefab`
		- `Props/` - 환경 소품 프리팹 (MeshCollider, Static). 예: `PropStonePillar.prefab`
		- `Weapons/` - 무기 외형 프리팹 (콜라이더 없음, 원점 = 손잡이). 예: `WeaponHolyMace.prefab`
		- `Enemies/` - 적 프리팹 (AI·체력·모델·EnemyAnimator). 예: `EnemyHeavenSoldier.prefab`, 정예는 `~Elite`
		- `Field/` - 필드 석판·다리 프리팹 (ProBuilder). 예: `FieldSlabRound8.prefab`
		- `Town/` - 마을 NPC 프리팹(`Npc{이름}`, TownSetupTools 생성), 명판(`ShopSign{종류}`), 상점 임시 건물. 상점 건물 모델은 `Props/PropShop{이름}`
	- `Models/` - 3D 모델 (FBX)
		- `<이름>/` - 캐릭터·소품별 FBX와 텍스처. 예: `Paladin/Paladin.fbx`, `PropPaifang/T_PropPaifang_BaseColor.png`. 소품은 `Prop`, 무기는 `Weapon`으로 시작 (리그 없이 임포트)
		- `Field/` - 필드용 생성 메시 (`CloudSeaGrid.asset` 등, 에디터 도구가 생성)
	- `Animations/` - 애니메이션 클립·애니메이터 컨트롤러
		- `<이름>/` - 캐릭터별 Mixamo 애니메이션 FBX와 Animator. 예: `Paladin/PaladinRun.fbx`, `Paladin/PaladinAnimator.controller`
		- `Npc/` - 마을 NPC 대기 Animator (`Npc{이름}Animator.controller`, TownSetupTools 생성)
	- `Effects/` - 파티클·이펙트
	- `Sprites/` - 이미지 (UI 아이콘 포함)
		- `Map/` - 맵별 지도 그림 (`T_Map_{맵}.png` 전체 2048², `T_Map_{맵}_Town.png` 마을 세부 1024², LevelTools 생성)
			- `Icons/` - 지도 아이콘 (`T_MapIcon{종류}.png`, 128px. Python 생성)
		- `Cursors/` - 마우스 커서 (`T_Cursor{Default|Attack|Talk|Blocked}.png`, 48px. Python 생성)
		- `Town/` - 상점 명판 이미지 (`T_ShopSign{Goods|Forge|Clinic}.png`, VARCO 이미지)
	- `Audio/` - 효과음·배경음
	- `Materials/` - 머티리얼
		- `Characters/` - 캐릭터 머티리얼 (URP Lit). 예: `M_Paladin.mat`
		- `Props/` - 환경 소품 머티리얼 (URP Lit). 예: `M_PropStonePillar.mat`
		- `Weapons/` - 무기 머티리얼 (URP Lit). 예: `M_WeaponHolyMace.mat`
		- `Field/` - 필드 머티리얼 (하늘·구름 바다·석판). 예: `M_FieldSkybox.mat`
		- `Town/` - 상점 명판 머티리얼 (`M_ShopSign{종류}.mat`, TownSetupTools 생성)
	- `Shaders/` - 셰이더 (UI·이펙트·캐릭터)
	- `Terrain/` - Terrain 데이터(`{맵}TerrainData.asset`), `Layers/` 지형 텍스처 레이어(`TL_`)·텍스처(`T_Terrain*`)
	- `Resources/` - 실행 중 이름으로 불러오는 에셋
		- `UI/` - 커서 규칙(`CursorSettings.asset`: 레이어별 커서), 지도 아이콘(`MapIconSet.asset`: 종류별 그림·표시 확대 단계). UiArtTools가 생성, HudView가 씬 연결이 없으면 자동으로 불러옴
	- `Settings/` - 렌더 파이프라인(URP) 설정, 입력 설정(`InputSystem_Actions.inputactions`), 필드 분위기 설정(`FieldLookProfile.asset`, `FieldVolumeProfile.asset`)
	- `ThirdParty/` - 외부에서 받은 에셋 원본
	- `Bad_Raccoon/` - Asset Store "3D Realistic Terrain Free" (임포트 기본 경로 유지). 라이선스·용량 때문에 `.gitignore`로 제외, 각 PC에서 Package Manager > My Assets로 임포트. 없으면 LevelTools가 코드 생성 산맥으로 대체

- 새 스크립트는 `Scripts/` 아래 기능 폴더에 둡니다. 맞는 폴더가 없으면 폴더를 새로 만들기 전에 묻습니다.
- `Assets/` 바로 아래에 파일을 두지 않습니다.
- 빈 폴더에는 `.gitkeep` 파일을 둡니다. Git은 빈 폴더를 저장하지 않아서, 폴더가 없으면 다른 PC에서 유니티가 폴더의 `.meta` 파일을 지우기 때문입니다. 폴더에 파일이 생기면 `.gitkeep`을 지웁니다.
- `ThirdParty/` 안의 파일은 수정하지 않습니다.
- `Tests/EditMode/`와 `Tests/PlayMode/`에는 각각 어셈블리 정의 파일(`.asmdef`)을 둡니다. 테스트 코드가 게임 빌드에 들어가지 않게 하기 위해서입니다.
- 게임 스크립트는 `Scripts/BrokenSovereign.asmdef` 한 개로 묶습니다. 테스트 asmdef(`BrokenSovereign.Tests.EditMode`, `BrokenSovereign.Tests.PlayMode`)는 `BrokenSovereign`을 참조합니다. 새 패키지를 쓰면 `BrokenSovereign.asmdef` 참조에 추가합니다.

## 씬

- 기능 개발은 `Scenes/Sandbox/`의 기능별 테스트 씬에서 합니다. 예: `ClickMoveTest.unity`
- 본 씬(`HeavenZone01.unity`~`HeavenZone03.unity`, `HeavenBoss.unity`)은 레벨 작업 브랜치에서만 수정합니다. 여러 브랜치에서 같은 씬을 고치면 병합 충돌이 나기 때문입니다.

## 큰 파일

- `.fbx`, `.wav`, 큰 텍스처 같은 바이너리 에셋은 Git LFS로 관리합니다. 대상 확장자는 `.gitattributes`를 따릅니다.
- 100MB가 넘는 파일은 커밋하지 않습니다. GitHub이 거부합니다.

## Docs 폴더

- `Docs/`
	- `Rules/` - 규칙 문서 (이 문서 포함)
	- `Backup/` - 문서 백업본 (날짜를 파일 이름에 붙임. 예: `GDD_2026-10-02.md`)
	- `GDD.md` - 기획 문서
	- `ClassDiagram.md` - 클래스 다이어그램
	- `AiPipeline.md` - AI 에셋·코드 생성 기록
	- `DevLog.md` - 개발 일지
	- `QAChecklist.md` - QA 체크리스트
	- `Mentoring.md` - 멘토링 질문

## Tools 폴더

- `Tools/` - 유니티 밖에서 쓰는 작업 도구 (`Assets/` 밖이라 빌드에 들어가지 않음)
	- `Blender/` - Blender 파이썬 스크립트. 예: `bs_model_cleanup.py` (VARCO 모델 정리·FBX 내보내기)

- 도구가 만든 중간 결과물(원본 모델, 작업용 `.blend`, Mixamo 업로드용 FBX)은 저장소 밖 `C:\github\K_ND\VarcoWork\`에 둡니다. 게임에 쓰는 최종 FBX만 `Assets/Models/`에 넣습니다.

## 에셋 이름

- 스크립트와 같은 규칙으로 PascalCase를 쓰고 공백·한글을 쓰지 않습니다.
- 생성형 AI로 만든 이미지·사운드·3D 모델도 같은 이름·폴더 규칙을 따릅니다. 사용 조건(상업 이용 가능 여부)을 확인한 뒤 넣고, 생성 기록은 `Docs/AiPipeline.md`에 남깁니다.

| 종류 | 예시 |
|---|---|
| 프리팹 | `EnemyHeavenSoldier.prefab` |
| 스프라이트 | `IconSkillOathStrike.png` |
| 사운드 | `SfxSmiteHit01.wav` |
| 씬 | `HeavenZone01.unity` |
| 3D 모델 | `ErlangShen.fbx` |
| 머티리얼 | `M_Paladin.mat` (여러 개면 `M_Paladin_01`) |
| 텍스처 | `T_Paladin_BaseColor.png`, `T_Paladin_Normal.png`, `T_Paladin_Roughness.png` |
| 애니메이션 | `PaladinDodge.anim` |
| 이펙트 | `VfxRadiantSmite.prefab` |
| ScriptableObject | `SkillOathStrike.asset`, `HeroSunWukong.asset` |

## 유니티 파일 다루기

- 유니티는 파일마다 `.meta` 파일을 만들어 파일끼리의 연결을 기억합니다. 파일을 옮기거나 이름을 바꿀 때는 탐색기가 아니라 유니티 에디터의 Project 창에서 합니다.
- `.meta` 파일은 원본 파일과 함께 커밋합니다.
- 씬(`.unity`)과 프리팹(`.prefab`) 파일은 텍스트로 직접 고치지 않습니다. 연결이 깨질 수 있으므로 에디터에서 작업합니다.

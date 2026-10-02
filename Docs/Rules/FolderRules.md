# 폴더 구조 규칙

스크립트·에셋을 새로 만들거나 옮길 때 따르는 규칙입니다. 규칙은 이 문서에만 적습니다.

## Assets 폴더

- `Assets/`
	- `Scripts/` - C# 스크립트
		- `Core/` - 이벤트 버스, 이벤트 클래스, 공용 유틸리티
		- `Player/` - 클릭 이동, 입력 처리, 쿼터뷰 카메라
		- `Combat/` - 체력, 피해 계산(순수 클래스), 상태이상
		- `Skill/` - 기술 정의·실행, 슬롯 레벨·강화 분기, 직업 자원(신앙 등)
		- `Ascension/` - 격 단계, 돌파 조건, 스탯 분배
		- `Hero/` - 영웅의 길, 영웅 고유 메커니즘 규칙 객체
		- `Relic/` - 신물창, 신화 세트 효과
		- `Equipment/` - 일반 장비창, 무기 계열 보너스
		- `Achievement/` - 업적, 시련
		- `Enemy/` - 적 AI, 보스, 격 수확 패턴
		- `UI/` - HUD, 캐릭터·신물·장비·스킬·업적 창
		- `Manager/` - 게임 상태, 체크포인트
	- `Data/` - ScriptableObject 에셋
		- `Skills/` - 기술 정의, 강화 분기
		- `Heroes/` - 영웅의 길 정의
		- `Relics/` - 신물 정의, 신화 세트
		- `Equipment/` - 일반 장비 정의
		- `Enemies/` - 적·보스 정의
		- `Ascension/` - 격 단계별 돌파 조건, 시련
		- `Balance/` - 신앙·스탯·피해 등 밸런스 수치
	- `Tests/` - 테스트
		- `EditMode/` - 순수 로직 테스트
		- `PlayMode/` - 플레이 흐름 테스트
	- `Scenes/` - 씬
		- `Sandbox/` - 기능별 테스트 씬
	- `Prefabs/` - 프리팹
	- `Models/` - 3D 모델 (FBX)
	- `Animations/` - 애니메이션 클립·애니메이터 컨트롤러
	- `Effects/` - 파티클·이펙트
	- `Sprites/` - 이미지 (UI 아이콘 포함)
	- `Audio/` - 효과음·배경음
	- `Materials/` - 머티리얼
	- `ThirdParty/` - 외부에서 받은 에셋 원본

- 새 스크립트는 `Scripts/` 아래 기능 폴더에 둡니다. 맞는 폴더가 없으면 폴더를 새로 만들기 전에 묻습니다.
- `Assets/` 바로 아래에 파일을 두지 않습니다.
- `ThirdParty/` 안의 파일은 수정하지 않습니다.
- `Tests/EditMode/`와 `Tests/PlayMode/`에는 각각 어셈블리 정의 파일(`.asmdef`)을 둡니다. 테스트 코드가 게임 빌드에 들어가지 않게 하기 위해서입니다.
- 게임 스크립트는 `Scripts/BrokenSovereign.asmdef` 한 개로 묶습니다. 테스트 asmdef는 `BrokenSovereign`을 참조합니다. 새 패키지를 쓰면 `BrokenSovereign.asmdef` 참조에 추가합니다.

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
	- `Mentoring.md` - 멘토링 질문

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
| 애니메이션 | `PaladinDodge.anim` |
| 이펙트 | `VfxRadiantSmite.prefab` |
| ScriptableObject | `SkillOathStrike.asset`, `HeroSunWukong.asset` |

## 유니티 파일 다루기

- 유니티는 파일마다 `.meta` 파일을 만들어 파일끼리의 연결을 기억합니다. 파일을 옮기거나 이름을 바꿀 때는 탐색기가 아니라 유니티 에디터의 Project 창에서 합니다.
- `.meta` 파일은 원본 파일과 함께 커밋합니다.
- 씬(`.unity`)과 프리팹(`.prefab`) 파일은 텍스트로 직접 고치지 않습니다. 연결이 깨질 수 있으므로 에디터에서 작업합니다.

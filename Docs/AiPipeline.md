# AI 활용 기록

- 작성 기준: 에셋·코드·문서를 AI로 생성할 때마다 기록. 도구·프롬프트·결과·사람 수정 내용 함께 기재
- 원칙: AI는 초안·후보·비교안 작성, 최종 결정은 개발자

## 1. 사용 도구

| 도구 | 용도 | 비고 |
|---|---|---|
| Claude (Cowork·Claude Code) | 기획 문서, 설계 문서, 코드 작성·검토, 저장소 관리 | Unity MCP로 에디터 연결 |
| VARCO 3D | 3D 모델 생성 | 10-15부터 사용. Text to 3D 플로우(텍스트 → GPT Image 1.5 이미지 → 3D). Claude가 앱 내장 브라우저로 프롬프트 입력·실행, 다운로드는 개발자 |
| VARCO Sound | 효과음 생성 | 사용 예정. Claude 커넥터·플러그인으로 연결 예정 |
| Blender | 생성 모델 수정 | 10-14 MCP 연결 (Blender 5.2). 정리 스크립트 `Tools/Blender/` 실행 |
| Mixamo | 자동 리깅·애니메이션 | 10-15부터 사용. Blender FBX·텍스처 포함 zip 업로드 실패, OBJ(메시만) 업로드로 확정 |

## 2. 에셋 생성 기록

| 날짜 | 도구 | 에셋 (파일) | 프롬프트 원문 | 생성 설정 | 사람 수정 (Blender 등) | Unity 임포트 설정 | 상업 이용 |
|---|---|---|---|---|---|---|---|
| 2026-10-15 | VARCO 3D → Mixamo → Blender | 성기사 `Paladin.fbx` (테스트, Unity 미반영) | Claude 프로젝트 문서 `BrokenSovereign_VARCO_프롬프트_1015.md` 1번(성기사) + 캐릭터 공통 조건 | 이미지 GPT Image 1.5 1:1 ×1, 3D Target Polycount 20000, Triangle, 텍스처 2K, PBR 켬, T-Pose 켬, Private | 정점 병합(448조각 → 1), 높이 1.8m·발바닥 피벗, 텍스처 3장 분리 저장, Mixamo 리깅(65본) 후 0.01배 스케일 보정, 웨이트 4본 제한·미세 웨이트 삭제·관절/허리 천 스무딩 | 미정 (Humanoid 예정) | VARCO 이용 약관 확인 필요 |
| 2026-10-15 | VARCO 3D → Blender | 환경 소품 4종 `PropStonePillar`·`PropPaifang`·`PropStoneLantern`·`PropCloudRailing` | Claude 프로젝트 문서 `BrokenSovereign_VARCO_프롬프트_1015.md` P1~P4 + 소품 공통 조건 | 이미지 GPT Image 1.5 1:1 ×1, 3D Target Polycount 5000 (난간 2000), Triangle, 텍스처 2K, PBR 켬, T-Pose 끔, Private. 4종 병렬 생성 | 높이·길이 맞춤(4m·6m·1.5m·길이 2m), 밑면 중앙 피벗, 패루·난간 90° 회전, 패루·난간 Decimate(5,698→4,850, 2,142→1,940), MetallicSmoothness 변환 | 리그 없음, 머티리얼 M_Prop*, 프리팹 MeshCollider·Static | VARCO 이용 약관 확인 필요 |
| 2026-10-15 | VARCO 3D → Mixamo → Blender | 천병 `HeavenSoldier` (Unity 미반영) | 같은 문서 2번(천병) + 캐릭터 공통 조건 | 성기사와 동일 (20000, T-Pose 켬) | 높이 1.8m·발바닥 피벗, Mixamo 리깅(65본) 후 스케일 보정, 웨이트 4본 제한·스무딩 | 10/23 적용 예정 | VARCO 이용 약관 확인 필요 |
| 2026-10-15 | VARCO 3D → Mixamo → Blender | 역사 `TempleGuardian` (Unity 미반영) | 같은 문서 3번 → **재작성**: 금강역사(Geumgang-yeoksa, Nio) 원형 명시, human face, no tusks, 팔 수평 강조 | 성기사와 동일 | 1차 이미지가 송곳니 있는 오크형·A포즈로 나와 개발자 요청으로 사람형 재생성. 높이 2.2m, Mixamo 리깅(65본), 웨이트 보정 | 10/23 적용 예정 | VARCO 이용 약관 확인 필요 |
| 2026-10-15 | VARCO 3D → Mixamo → Blender | 이랑진군 `ErlangShen` (Unity 미반영) | 같은 문서 4번(이랑진군) + 캐릭터 공통 조건 | 성기사와 동일 | 높이 2.4m. Mixamo가 손가락을 구분하지 못해 Standard 선택에도 33본(검지만) → `bs_finger_graft.py`로 메시 형태 분석해 다섯 손가락 본 이식(65본)·손 웨이트 재분배, 주먹 포즈 확인. 웨이트 보정 | 10/23 적용 예정 | VARCO 이용 약관 확인 필요 |
| 2026-10-15 | VARCO 3D → Blender | 무기 2종 `WeaponHolyMace`(성기사 철퇴)·`WeaponCelestialStaff`(성투사 장봉) | 철퇴: 백옥 플랜지 머리·금 테두리·태양 문양·가죽 손잡이, 80cm / 장봉: 붉은 옻칠 봉·양 끝 금 캡·금 고리, 180cm + 무기 공통 조건(단독 소품, 세워진 상태, 손·캐릭터 없음) | 이미지 GPT Image 1.5 ×1, 3D Target Polycount 3000, Triangle, 텍스처 2K, PBR 켬, T-Pose 끔. 2종 병렬 생성 | 길이 맞춤(0.8m·1.8m), 원점 = 손잡이(높이 비율 0.18·0.35), MetallicSmoothness 변환 | 리그 없음, 머티리얼 M_Weapon*, 프리팹 콜라이더 없음. 손잡이 위치·회전은 성기사 T포즈 오른손에서 자동 계산 | VARCO 이용 약관 확인 필요 |
| 2026-10-19 | VARCO 3D → Blender (→ Mixamo 예정) | 마을 NPC 3종 `NpcMerchant`·`NpcBlacksmith`·`NpcHealer` | Claude 프로젝트 문서 `BrokenSovereign_VARCO_프롬프트_1015.md` N1~N3 + 캐릭터 공통 조건 | 플로우 Text to 3D, 이미지 GPT Image 1.5 1:1 ×1, 3D Target Polycount 30000, Triangle, 텍스처 4K, PBR 켬, T-Pose 켬, Private. 3종 병렬 생성 (Claude가 내장 브라우저로 실행, 다운로드는 개발자) | 높이 1.7m·1.85m·1.75m, 발바닥 피벗, 텍스처 2048 축소, 정면 확인(렌더), Mixamo 리깅(상인 65본, 치유사 33본·대장장이 41본 → 손가락이 가늘거나 장갑이라 Mixamo가 구분 실패, 미리보기에서 손가락 휨) → `bs_rig_quality.finalize_npc`: 0.01배 보정, 다섯 손가락 본 이식(65본), 손가락별 웨이트 분리(다른 손가락끼리 섞지 않음), 몸 웨이트 보정, 손가락 굽힘·팔 내림 렌더로 확인. FBX 1.3MB | 대기 동작은 성기사 대기 동작 Humanoid 공유, `TownSetupTools`가 NPC 프리팹 생성 | VARCO 이용 약관 확인 필요 |
| 2026-10-19 | VARCO 3D → Blender | 상점 건물 3종 `PropShopGoods`·`PropShopForge`·`PropShopClinic` | 같은 문서 S1~S3 + 건물 공통 조건 | 3D Target Polycount 20000, Triangle, 텍스처 4K, PBR 켬, T-Pose 끔, Private | 4방향 렌더로 앞면 판별 → 잡화·대장간 -90° 회전, 가로 5m·6m·5m, 밑면 중앙 피벗, 텍스처 2048 축소 | 리그 없음, Props 메뉴로 프리팹(MeshCollider), LevelTools가 광장을 바라보게 배치 | VARCO 이용 약관 확인 필요 |
| 2026-10-19 | VARCO 이미지 | 상점 명판 3종 `T_ShopSign{Goods,Forge,Clinic}.png` | 글자 없는 금테 판 + 금빛 아이콘(주머니·항아리 / 검·방패 / 호리병·연꽃), 바탕은 지붕 색 계열. Generate Text 노드에 "프롬프트 그대로 출력" 지시 | 플로우 Text to Image, GPT Image 1.5, 4:3 ×2 (결과 1536×1024) | 상점별 2장 중 아이콘이 크고 판을 채운 것 선택, 투명 여백 잘라냄 | 명판 프리팹 `ShopSign*`(이미지 비율로 세로 길이 계산, 약한 발광) | VARCO 이용 약관 확인 필요 |

- 에셋 파일 이름·폴더: `Docs/Rules/FolderRules.md` '에셋 이름' 기준
- 상업 이용 조건 확인 후 프로젝트 반영

## 3. 코드·문서 생성 기록

### 3.1 기획

- 날짜: 10월 초 (근거: GDD 백업본 `Docs/Backup/GDD_2026-10-02.md`)

| 항목 | AI 작업 | 개발자 결정 |
|---|---|---|
| 영웅 16인 선정 | 직업별 후보 영웅·성물·고유 메커니즘 제시, 권역 균형·메커니즘 중복 점검 | 빌헬름 텔 제외 → 예(羿), 로빈 후드 → 이반, 눈의 여왕 → 항아, 모모타로 제외, 목란 성물 쌍토검 확정 |
| 마법사 2차 직업 | 파우스트와 '비전술사' 이름 불일치 분석, 직업명 변경과 영웅 교체 비교 | 직업명 '현자'로 변경 |
| 원소술사 원소 | 불 + 번개 후보 영웅 비교 | 멀린 유지, 파에톤(불 + 번개) 확정 |
| 기술 빌드 구조 | 디아블로식 해금과의 차이 분석, B안·C안·정리안 비교 | '격 회복형' 정리안 채택, 분기 재선택 B안 (비전투 시 재선택) |
| 신앙 자원 | 전투 용도·충만 규칙 초안, 세계관 설정(영웅 신앙) 제안 | 충만 A안, 격 회복마다 최대치 증가 요청 |
| 무기 장착 | 전직 후 무기 제한 방식 비교 | B안 (1차 직업 단위 허용 + 2차 직업 주 계열 보너스) |
| 프로토타입 보스 | 권속·하위 신 후보, 패턴 방향 | 우사·풍백 2인 1조, AI 공유 + 이펙트 분리 구조 직접 제안, 이랑진군 확정 |
| 손오공 4번 기술 | 회전 이동 기술 설계 (대뇨천궁) | 휠윈드형 기술 요청, 반경 증가는 강화 분기로 이동 |
| 신물 부위 순서 | 격 단계별 부위 순서안 | A안 (갑옷 → 장신구 → 무기 → 장갑 → 신발 → 투구) |
| 영웅 명명·성투사 교체 (10-14) | 헤라클레스와 성기사 컨셉 불일치 분석, 투전승불 축 후보 비교, 실명 대신 원형 레퍼런스 방식의 장단점·범위 비교, 16인 전투 스타일 점검표 작성, 인내 축적안 중복 발견 | 헤라클레스 → 야로미르(원형 일리야 무로메츠) 교체, 영웅 16인 창작 이름 전환(B안, 주신·하위 신 실명 유지), 고유 메커니즘 분쇄(무력화) 확정 |

### 3.2 설계·코드

| 날짜 | 도구 | 대상 | AI 작업 | 개발자 결정·수정 |
|---|---|---|---|---|
| 2026-10-02 | Claude | `Docs/ClassDiagram.md` | 프로토타입 범위 클래스 다이어그램 15개 초안, 이벤트 목록, 테스트 대상 | 직업 정의 위치, 시전 전 수치 변경 연결 지점(`ISkillParameterModifier`) 도입, 이벤트 폴더 신설, 클릭 이동 방식 결정 |
| 2026-10-02 | Claude | 프로젝트 구조 | 폴더·asmdef 생성, 템플릿 정리, 줄바꿈 규칙 추가 | 네임스페이스 규칙, 입력 설정 위치, 샘플 씬 유지 결정 |
| 2026-10-14~15 | Claude | `Tools/Blender/bs_model_cleanup.py`, `bs_rig_quality.py` | VARCO 모델 정리(파편·폴리곤·크기·피벗·머티리얼·FBX), Mixamo 업로드 파일·리깅 결과 마무리, 시험 포즈 기반 스키닝 측정·보정 | 정리 범위·프리셋 크기 승인, Mixamo 업로드 형식 시행착오 공유, 리깅 품질 개선 요청 |
| 2026-10-15 | Claude | `Tools/Blender/bs_finger_graft.py`, `bs_rig_quality.finalize_and_improve` | 메시 연결 덩어리·k-means로 손가락 위치 추정 후 본 체인 생성, 손 웨이트 거리 기반 재분배 / 리깅 마무리 일괄 처리 | 보스 품질 요구로 손가락 본 복원 결정 |
| 2026-10-15 | Claude | `Scripts/Equipment/`(`WeaponDefinition`·`PlayerWeapon`·`WeaponType`), `JobDefinition` 기본 무기, `Editor/WeaponSetupTools.cs` | 무기별 기본 공격 간격(IAttackIntervalSource), 직업 기반 무기 교체, 오른손 본 장착, 무기 에셋·직업 연결 에디터 메뉴, Blender 손잡이 피벗 옵션 | GDD 11장 기준 직업이 무기를 결정(장비창 없음), 성기사 철퇴 1.0초·성투사 장봉 1.3초 결정 |
| 2026-10-15 | Claude | `Scripts/Hero/`(`HeroPathDefinition`·`HeroPathController`·`HeroRule` 뼈대), `UI/HeroPathSelectView`, `Editor/HeroSetupTools`, `PlayerProgression`·`PlayerJob` 연결 | 반신 돌파 후 영웅의 길 선택 요청·선택 화면·영웅 기술 재장착, 폴더 의존 규칙 위반(Equipment↔Ascension) 발견·정리, GDD 기준 영웅 기술 4번 슬롯 판단과 QA 항목 정정 제안 | 10/19 일정을 앞당겨 진행 결정, 플레이 동작 확인 |
| 2026-10-15 | Claude | `Enemy/EnemyAnimator`, `Editor/EnemySetupTools`, `CharacterSetupTools.BuildModelAssets`, `bs_rig_quality` 텍스처 미포함 내보내기 | 천병·역사 모델 Unity 적용(Run은 성기사 모션 Humanoid 재사용), 적 3종 데이터·프리팹 생성 도구, 역사·정예 수치 초안, 텍스처 포함 FBX(12MB) 발견 후 재내보내기(0.97MB, 본 위치 차이 0.000001m 검증) | 적 2종 + 정예 계획 승인, 수치 초안 승인 |
| 2026-10-16 | Claude | `Editor/LevelTools.cs`, `FieldTools`(분위기·카메라 공개, 구름 바다 크기 자동 확장) | 맵 3개(구역 1·구역 2·보스 방) 레이아웃(방=원, 길=선분, 다리 연결, 디아블로4 던전 규모 420m·통로 약 880m·곁길·통로 적 무리 지점), 위에서 본 레이아웃 미리보기로 섬 분리·겹침 검증, 부유 섬 Terrain 높이맵·텍스처 칠하기 자동 생성, 지형 텍스처 4종 코드 생성(이음새 없는 노이즈), 석등·석주·패루·표시 오브젝트 배치, NavMesh 굽기 | 맵 구성(GDD 구역 3개)·부유 섬 Terrain + 석판 혼합·자동 생성 후 브러시 수정 방식 결정, 일반 맵을 디아블로4 던전 규모로 확대 요청(400m급 선택) |
| 2026-10-19 | Claude | `Editor/LevelTools.cs` 오픈 필드 재작성 | 1km 부유 대륙(대륙 기복·능선 노이즈 산줄기·해안 절벽·협곡) 높이맵, 가운데 마을(ProBuilder 임시 집·관문 패루·NPC·웨이포인트 표시), 구역 1·2를 지역으로 배치(길 총길이 약 1.9km), 지형 높이를 따르는 배치·기울어진 다리, 위에서 본 미리보기로 레이아웃 검증 | 지형 컨셉 변경(마을 중심 디아블로4식 거대 지형), 오픈 필드 1장 + 보스 방·부유 대륙·1km·당일 진행 결정 |
| 2026-10-19 | Claude | `Scripts/Town/`(`TownNpc`·`NpcInteractor`·`INpcService`·`HealerService`), `UI/NpcDialogView`, `Editor/TownSetupTools`, 입력 Interact(F), `PotionUser.Refill` | 마을 상점 3종 설계(건물 + NPC + 명판), F 대화·치유사 회복, 건물 모델 크기를 재서 NPC·명판 위치 자동 계산, VARCO 생성 실행·결과 선별, 명판 이미지 잘못 받은 파일 발견 | 상점이 단출하다는 피드백 → NPC·상점 건물·명판 아이콘 요청, 범위(외형 + 간단 상호작용)·VARCO 신규 생성·크레딧 사용 승인 |
| 2026-10-19 | Claude | `Editor/LevelTools.Town.cs`(마을), `LevelTools.Valley.cs` 지도 그림, `Core/GameSettings`, `UI/MapLayer`·`MapIconSet`·`CursorSettings`·`CursorController`, `Editor/UiArtTools`, 커서·지도 아이콘 이미지(Python 생성) | 참고 이미지(디아블로4 마을 지도)를 길 규칙(광장·십자 큰길·네모 길·대각선)으로 옮김, 길과 겹치지 않는 격자 집 배치·건물 자리 기록 → 지도에 표시, 지도 그림 양피지 색조·마을 세부 그림, 전체 화면/오버레이 지도·휠 확대·확대 단계별 아이콘, 레이어별 커서, 오브·미니맵 테두리 축소, 경험치 바 테두리 | 커서·마을·지도 화면·테두리 수정 요청, 출구 4개·전체 화면 기본·오버레이 선택(추후 시스템 설정) 결정 |
| 2026-10-19 | Claude | `Editor/LevelTools.Valley.cs`(계곡 지형·강·걷기 메시·풀·바위·지도 그림), `Shaders/RiverWater.shader`, `Scripts/Map/`(`LevelMap`·`MapMarker`), `UI/MinimapView`·`WorldMapView`·`MapIcons` | 참고 사진(티베트 계곡)을 높이 규칙으로 옮김(산맥 벽·능선 산·초원 계곡·자갈 강), 위에서 본 미리보기(Python)로 걷기 영역·여울 검증, 산·강 진입 차단을 경사 대신 걷기 메시로 처리, 미니맵 그림을 실시간 카메라 대신 생성 때 1장으로 | 지형이 밋밋하다는 피드백과 참고 이미지, 산맥 분지·여울·원형 미니맵 + M 전체 지도·일정 조정(석천 10/20) 결정 |

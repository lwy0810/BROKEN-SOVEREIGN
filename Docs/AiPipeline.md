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

# 개발 일지

- 작성 기준: 작업 세션마다 갱신. 날짜 최신순
- 항목: 한 일, 막힌 점, 원인, 해결, 다음

## 2026-10-20

| 구분 | 내용 |
|---|---|
| 한 일 | 오픈 필드 가도 표시·성문 적·플레이어 세트 자동 연결, 커서 Resources 연결, 적 애니메이터 복구 (PR #42~#44). 석천 사거리 전환(5번 모드변환·좌클릭 수정치)·4번 난천무(지속 시전), 야로미르 분쇄(버팀 게이지·무력화·4번 분쇄 일격·버팀 막대). 기술별 시전 모션 구조(`SkillDefinition.AnimationState`, `PaladinSkill*.fbx` 자동 상태 생성, 지속 시전 반복 모션) |
| 막힌 점 | ① `LevelTools.Player.cs`에서 `PivotToFeet` 없음 컴파일 오류<br>② 좌클릭 기본 공격은 기술이 아니라 설계의 `ISkillParameterModifier`로 수치를 바꿀 수 없음<br>③ 숫자 키 유지는 매 프레임 재시전 구조라 "누르는 동안 지속"을 표현할 수 없음 |
| 원인 | ① 10/19 발 높이 보정 함수가 커밋 전에 사라짐<br>②·③ 기존 구조가 즉발 기술 반복 시전 기준 |
| 해결 | ① `PivotToFeet` 복구<br>② 기본 공격 전용 수정치 연결 지점 `IBasicAttackModifier` 추가 (영웅별 분기 없음)<br>③ `SkillDefinition` 지속 시전 옵션 + `SkillCaster` 유지 갱신·`ReleaseChannel` |
| 다음 | 10/21 이랑진군 + 알파 QA. 석천·야로미르 수치·연출 점검 |

## 2026-10-02

| 구분 | 내용 |
|---|---|
| 한 일 | GDD 작성 및 프로토타입 범위 확정, 저장소 규칙 문서·에이전트 설정 (PR #1), 클래스 다이어그램 작성 (PR #2), 프로젝트 폴더 구조·asmdef·템플릿 정리 (PR #3), 개발 일지·AI 활용 기록 문서 생성 |
| 막힌 점 | PR #1 머지 후 pull 거부. 로컬 main에 '수정됨' 파일이 남고 GitHub Desktop Discard 무반응 |
| 원인 | ① `.gitignore`·`Packages/manifest.json`·`Packages/packages-lock.json`·`PC_RPAsset.asset` 줄바꿈 차이 (저장소 LF, 작업 폴더 CRLF), `.gitattributes`에 해당 규칙 없음<br>② `URP.png`가 첫 커밋에 LFS 포인터가 아닌 원본으로 저장 → git-lfs가 항상 변경으로 판정 |
| 해결 | 파일 4개 저장소 버전 복원 후 `origin/main`으로 fast-forward 병합, 남은 스태시 삭제. 재발 방지로 `.gitattributes`에 `text` 규칙 추가 (PR #3) |
| 다음 | 10/05부터 1주차 전투 기반. 클릭 이동·쿼터뷰 카메라 우선 (`feature/click-move`) |

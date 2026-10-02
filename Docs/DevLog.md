# 개발 일지

- 작성 기준: 작업 세션마다 갱신. 날짜 최신순
- 항목: 한 일, 막힌 점, 원인, 해결, 다음

## 2026-10-02

| 구분 | 내용 |
|---|---|
| 한 일 | GDD 작성 및 프로토타입 범위 확정, 저장소 규칙 문서·에이전트 설정 (PR #1), 클래스 다이어그램 작성 (PR #2), 프로젝트 폴더 구조·asmdef·템플릿 정리 (PR #3), 개발 일지·AI 활용 기록 문서 생성 |
| 막힌 점 | PR #1 머지 후 pull 거부. 로컬 main에 '수정됨' 파일이 남고 GitHub Desktop Discard 무반응 |
| 원인 | ① `.gitignore`·`Packages/manifest.json`·`Packages/packages-lock.json`·`PC_RPAsset.asset` 줄바꿈 차이 (저장소 LF, 작업 폴더 CRLF), `.gitattributes`에 해당 규칙 없음<br>② `URP.png`가 첫 커밋에 LFS 포인터가 아닌 원본으로 저장 → git-lfs가 항상 변경으로 판정 |
| 해결 | 파일 4개 저장소 버전 복원 후 `origin/main`으로 fast-forward 병합, 남은 스태시 삭제. 재발 방지로 `.gitattributes`에 `text` 규칙 추가 (PR #3) |
| 다음 | 10/05부터 1주차 전투 기반. 클릭 이동·쿼터뷰 카메라 우선 (`feature/click-move`) |

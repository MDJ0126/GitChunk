# ResourceSplitter → GitChunk 변경 점검

> 이 문서는 이전 시험 배포 당시의 점검 기록입니다. 아래 1.2.4 버전과 릴리스 링크는 당시 기록이며, 현재 배포는 v1.0.0으로 다시 시작합니다. 현재 설치 방법은 [README](../README.ko.md)를 참고하세요.

## 확인된 이전 이름 사용처와 변경

| 사용처 | 변경 |
| --- | --- |
| `GitIntegration.cs` 포인터 생성 | `resourcesplitter-pointer-v1` → `gitchunk-pointer-v1` |
| clean/smudge 임시 파일 | `ResourceSplitter-*` → `GitChunk-*` |
| 새 조각의 GC 보호 참조 | `refs/resourcesplitter/cache/` → `refs/gitchunk/cache/` |
| 수동 분할 설정·manifest | 새 파일은 `GitChunk.settings.json`, `GitChunk.manifest.json` 사용 |
| 소스·프로젝트 파일 | `Tools/GitChunk/GitChunk.csproj`로 변경 |
| Windows/macOS 빌드, 설치, 패키징, CI | 변경된 소스 경로 사용 |
| `.gitattributes`, `.gitignore`, 예제 안내·생성기 | 이전 활성 설정·경로 제거 또는 변경 |
| 테스트의 새 포인터 기대값 | `gitchunk-pointer-v1`로 변경 |
| 배포용 EXE와 로컬 Git 필터 EXE | 변경된 소스로 다시 빌드하여 교체 |

이름과 포인터 형식, 저장 경로, 설정 파일명, 호환성 식별자는
`Tools/GitChunk/ProjectIdentity.cs`에서 관리합니다.
기존 포인터를 clean으로 다시 저장해도 새 헤더로 변환합니다.

## 현재 남아 있는 이전 이름

- `ProjectIdentity.cs`: `LegacyName`, `LegacyId`. 과거 포인터·저장소·실행 파일·필터·훅·설정·manifest 인식에 사용합니다.
- `Test-Identity.py`, `Test-Rules.py`, `Test-Uninstall.py`: 이전 형식 복원·이전 저장소 이동·필터 제거 검증용입니다.
- `INSTRUCTIONS.md`, `TestProject/테스트방법.txt`, 이 보고서: 기존 데이터 호환성 설명입니다.
- `.git/config`의 `filter.resourcesplitter.*`: 과거 커밋의 `.gitattributes`가 사용하는 필터이므로 유지합니다. 현재 작업 트리는 `filter=gitchunk`를 사용합니다.
- 기존 Git 커밋과 로컬 캐시 참조: 과거 데이터입니다. 이력을 다시 쓰거나 기존 GC 보호 참조를 삭제하지 않았습니다.
- 무시되는 `bin/`, `obj/`에는 이전 빌드·테스트 자료가 남아 있을 수 있습니다. 현재 소스 및 배포 입력은 `Tools/GitChunk`입니다.

첨부 화면의 `BP_HackAndSlash/BladeSlash.zip` 원격 파일은 이 작업으로 변경되지 않습니다.
새 실행 파일을 해당 저장소에 적용하고 다시 스테이징·커밋해야 GitHub에 새 헤더가 표시됩니다.

`1.2.4` 배포 후 해당 프로젝트의 Git 루트에서 다음 순서로 적용합니다.

```powershell
git chunk update
git chunk version
git add --renormalize .
git add .gitattributes .gitignore .gitchunk .chunkignore
git diff --cached
git commit -m "Migrate current pointers to GitChunk"
git push
```

업데이트 직후 과거 커밋의 포인터 이름은 유지됩니다. 재정규화는 현재 파일의 헤더를
변경하며 원본 데이터와 조각 ID는 유지합니다. 업데이트 후에는 버전이 `1.2.4`인지 확인합니다.

## 검증

Windows GUI/CLI 빌드와 새 이름·이전 포인터/저장소/설정/manifest 호환성 테스트를 수행했습니다.
규칙·clone·과거 이력·GC, 수동 분할·복원, 설치 제거, Git push/pull/checkout/rebase/stash, LFS 전환 회귀 테스트도 통과했습니다.
공개 배포 워크플로에서도 Windows와 macOS ARM64/x64의 검사가 모두 통과했습니다.
`1.2.4` 릴리스와 Windows 설치 파일·포터블 ZIP·macOS ZIP·SHA256SUMS를 발행했고,
Scoop/Homebrew 패키지 정의도 `1.2.4`로 갱신했습니다.

- 릴리스: https://github.com/MDJ0126/GitChunk/releases/tag/v1.2.4
- 검증·발행 결과: https://github.com/MDJ0126/GitChunk/actions/runs/38171009881

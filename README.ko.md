# Git Chunk

[English](README.md)

**Git LFS 없이 대용량 리소스를 관리합니다.** **50 MiB 초과** 파일은 Git에 최대 **49 MiB** 조각으로 저장하고, 로컬에서는 원본으로 작업합니다. .NET SDK는 필요 없습니다.

![원본 파일을 Git에 조각으로 저장하고 checkout 시 원본으로 복원합니다.](docs/assets/git-chunk-overview.png)

## Git LFS와 비슷한 구조, 다른 저장 방식

두 도구 모두 **Git에는 포인터를 커밋하고, 로컬에서는 원본 파일로 작업**합니다. 필터와 훅으로 일반 Git 작업에 연동되는 점도 같습니다. Git Chunk의 핵심 차이는 **실제 데이터를 별도 LFS 저장소 대신 같은 Git 저장소에 조각으로 저장**한다는 것입니다.

| 항목 | Git LFS | Git Chunk |
| --- | --- | --- |
| 기본 구조 | 포인터와 실제 데이터 분리 | 포인터와 실제 데이터 분리 |
| 원본 경로에 커밋되는 내용 | 파일 해시·크기를 담은 포인터 | 원본·조각의 해시와 크기를 담은 포인터 |
| 로컬 작업 폴더 | 원본 파일 | 원본 파일 |
| Git 작업 연동 | 필터와 훅으로 자동 처리 | 필터와 훅으로 자동 처리 |
| 실제 데이터 저장 | 별도 LFS 저장소 | **같은 Git 저장소의 `.gitchunk/objects`에 조각으로 저장** |
| 데이터 단위 | 전체 파일 객체 | **50 MiB 초과 파일을 최대 49 MiB씩 분할** |
| 데이터 전송 | Git 이력과 LFS 데이터를 별도로 전송 | **Git push·pull로 이력과 조각을 함께 전송** |
| 서버 요구사항 | LFS 엔드포인트 필요 | **일반 Git 서버 사용 가능, 별도 LFS 서비스 불필요** |
| 중복 데이터 재사용 | 동일한 전체 파일 객체 재사용 | **동일한 조각 재사용** |
| 과거 데이터·용량 | 실제 데이터는 LFS에 보관, LFS 용량 정책 적용 | **과거 조각도 Git 이력에 포함, 일반 Git 용량 정책 적용** |

**분할해도 전체 리소스 크기가 줄거나 저장소 제한이 없어지지는 않습니다.** 리소스의 변경 이력이 Git에 남아 clone 용량이 커질 수 있습니다. 전환한 파일은 LFS 저장 공간을 사용하지 않지만, 기존 프로젝트의 과거 LFS 객체는 전환만으로 삭제되지 않습니다.

참고: [Git LFS 공식 문서](https://github.com/git-lfs/git-lfs/blob/main/docs/man/git-lfs.adoc), [LFS 서버 설정](https://github.com/git-lfs/git-lfs/blob/main/docs/api/server-discovery.md), [GitHub LFS 요금 정책](https://docs.github.com/en/billing/concepts/product-billing/git-lfs).

## 설치

**Windows:** [설치 파일 다운로드](https://github.com/MDJ0126/GitChunk/releases/latest) 또는 Scoop을 사용하세요.

```powershell
scoop bucket add gitchunk https://github.com/MDJ0126/GitChunk.git
scoop install gitchunk/git-chunk
```

**Mac:** Homebrew를 사용하세요.

```bash
brew tap MDJ0126/gitchunk https://github.com/MDJ0126/GitChunk
brew install --cask MDJ0126/gitchunk/git-chunk
```

## 시작하기

프로젝트의 Git 저장소 최상위 폴더에서 한 번 실행하세요. 새 PC에서 clone한 뒤에도 필요합니다.

```bash
git chunk install
```

기존 추적 파일이 있다면 설정을 적용하고 커밋하세요.

```bash
git add -u
git add --renormalize .
git add .gitattributes .gitignore .gitchunk
git commit -m "Configure Git Chunk"
```

이후 **add·commit·push·pull·checkout**은 평소처럼 사용하시면 됩니다. 앱에서 프로젝트를 선택하고 **Install Git Chunk**를 눌러 설정할 수도 있습니다.

분할하지 않을 파일은 `.chunkignore`에 `Build/**` 같은 패턴을 적어 원본 그대로 저장하세요. `.gitchunk/objects`는 삭제하지 마세요.

## Git LFS 프로젝트 전환

**v1.1.0 이상**이 필요합니다. 먼저 추적 파일의 변경을 커밋하거나 stash하세요. 누락된 원본을 다운로드하려면 Git LFS와 원격 접근 권한이 필요합니다.

```bash
git chunk install
git diff --cached
git commit -m "Convert current LFS files to Git Chunk"
git push
```

설치가 현재 LFS 파일을 전환하고 스테이징합니다. 커밋·푸시는 자동으로 하지 않습니다. **과거 LFS 이력과 서버 저장 용량은 유지됩니다.** 전환 제한과 용량 정리 방법은 [상세 안내](docs/guide.ko.md#기존-git-lfs-프로젝트-전환)를 참고하세요.

## 업데이트

```bash
git chunk update
```

저장소 최상위 폴더에서 실행하세요. 이 명령이 없는 구버전은 설치 파일이나 패키지 관리자로 먼저 업그레이드해야 합니다.

[상세 안내 및 제거 방법](docs/guide.ko.md) · [배포 정보](Packaging/README.md) · [기술 설명](INSTRUCTIONS.md)

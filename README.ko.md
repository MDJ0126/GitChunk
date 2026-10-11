# Git Chunk

빌드 없이 설치할 일반 사용자용 Windows 설치 프로그램과 Mac 앱을 [v1.0.0부터 배포](https://github.com/MDJ0126/GitChunk/releases/latest)합니다. Scoop·Homebrew 명령어 설치는 [배포 방법](Packaging/README.md)을 참고하세요. 사용자 PC에 .NET SDK는 필요 없습니다.

[English](README.md) · **한국어**

앱 기본 언어는 영어입니다. 아래 설명의 설치 버튼은 **Install Git Chunk**, 규칙 저장은 **Save rules**, 프리셋 저장은 **Save preset**입니다.

명령어 설치도 지원합니다. Windows의 Install.ps1과 Mac의 install.sh가 소스를 가져와 CLI를 빌드·설치합니다. Git과 .NET 9을 빌드할 수 있는 SDK가 필요하며, EXE를 직접 다운로드하거나 GUI를 열 필요는 없습니다. 내부 실행 파일은 자동으로 설치됩니다. 설치 후 `git chunk install`, `git chunk status`, `git chunk rules`를 사용할 수 있습니다. 다운로드 명령은 스크립트 커밋·푸시 후 사용 가능하며 [영문 README의 설치 명령](README.md#command-line-installation)을 참고하세요.

대용량 파일을 일반 Git 안에 조각으로 보관합니다. Git LFS처럼 저장소마다 한 번 설치하고, 이후 평소처럼 add·commit·push·pull을 사용합니다. Git LFS 서버나 별도 계정은 필요하지 않으며 전체 데이터 용량을 줄이는 도구는 아닙니다.

## 설치

Windows에서 **GitChunk.exe**를 실행하고 Git 저장소 최상위 폴더를 선택하세요. 화면에서 분할 규칙을 편집하거나 프리셋을 불러온 뒤 **Git Chunk 설치**를 누릅니다. 이미 설치되면 설치됨 안내와 업데이트/재설치 버튼이 표시됩니다.

최초 설치 또는 기존 파일의 분할 정책 변경 후 저장소에서 실행합니다.

```bash
git add -u
git add --renormalize .
git add .gitattributes .gitignore .gitchunk
git commit -m "Configure Git Chunk"
git push
```

새 PC에서 clone한 뒤에도 앱으로 한 번 설치하세요. 최초 받은 복원 정보도 원본으로 복원합니다. 앱에는 수동 분할·복원 메뉴가 없습니다.

PowerShell 명령행 설치:

```powershell
./GitChunk.exe git-install C:/path/to/project | Out-Host
```

## 분할 규칙과 프리셋

현재 규칙은 `.gitchunk/rules.txt`, 프리셋은 `.gitchunk/presets/*.txt`에 저장합니다. GUI에서 규칙 저장, 프리셋 저장 및 불러오기를 지원하며 파일도 직접 편집할 수 있습니다. 프리셋을 불러온 뒤 **규칙 저장**을 눌러 현재 정책으로 적용합니다. 설치할 때는 편집한 규칙도 저장합니다.

```gitignore
# 대상 지정
Assets/**
*.psd
*.wav
# 제외 — 뒤에 있는 규칙이 우선
!Assets/Small/**
```

- `*`: /를 제외한 문자, `**`: 하위 경로 포함, `?`: 한 문자.
- / 없는 패턴은 어느 폴더의 이름에도 적용합니다. `/Assets/**`는 저장소 루트 기준입니다.
- `#`로 시작하는 줄은 주석, `!`로 시작하면 제외입니다. 일치하는 마지막 규칙이 우선하며 미일치 파일은 제외됩니다.
- 기본 프리셋 `default`는 `*`로 모든 대용량 파일을 지정합니다. `unity`는 Assets, `unreal`은 Content 아래를 지정합니다.
- 이는 .gitignore와 비슷한 제한된 문법입니다. 이스케이프·문자 클래스·여러 줄 패턴은 지원하지 않습니다. 경로에는 /를 사용하세요.
- 대상이면서 **50 MiB 초과**인 파일만 49 MiB 조각으로 분할합니다. `.meta`, Git 설정, 도구 및 두 데이터 폴더는 항상 제외됩니다.
- 규칙 밖 파일은 일반 Git 파일로 저장되므로 GitHub의 파일 크기 제한을 따릅니다. 100 MiB 이상 일반 blob은 훅에서 차단합니다.

규칙을 바꿔도 과거 복원 정보는 계속 복원됩니다. 기존 추적 파일의 저장 형식을 바꾸려면 `git add --renormalize .` 후 커밋하세요. 설정과 프리셋도 커밋해 팀과 공유합니다.

```text
.gitchunk/
  rules.txt
  presets/default.txt
  presets/unity.txt
  presets/unreal.txt
  presets/my-project.txt
  GitChunk.exe                 # 다른 PC 설치용
  objects/<blob-id>            # 실제 데이터 조각
```

## 저장 방식과 호환성

로컬에는 원본을 유지합니다. Git clean 필터가 Git 내부에만 작은 복원 정보를 저장하고, pre-commit 훅은 그 정보가 참조하는 조각을 같은 커밋에 포함합니다. smudge 필터는 조각/전체 SHA-256을 검증한 뒤 실제 원본으로 복원합니다. `.gitchunk/objects`를 임의로 삭제하지 마세요.

기존 ResourceSplitter의 pointer-v1 프로토콜과 `.resourcesplitter`에 보관된 과거 커밋을 지원합니다. 설치하면 legacy 필터도 새 실행 파일로 연결하고 관리 훅을 업데이트합니다. 기존 로컬 조각은 blob ID로 검증한 뒤 `.gitchunk/objects`로 복사하며, 이전 폴더 전체는 Git 내부 메타데이터에 백업합니다. 실행 중인 구버전 앱이 폴더 이동을 막으면 닫고 재설치하세요. 새 커밋이 참조하는 조각은 `.gitchunk/objects`에 저장하며 과거 커밋의 조각은 Git 이력에 유지됩니다.

활성 실행 파일은 Git 메타데이터의 `gitchunk` 디렉터리에도 보관하므로 설치 이전 커밋으로 체크아웃해도 실행 파일이 제거되지 않습니다. `.resourcesplitter` 폴더를 직접 이름만 바꾸지 말고 새 앱으로 설치하세요.

이전 수동 split/join 명령은 복구 호환 용도로만 유지합니다. 새 규칙은 Git clean 필터에 적용되며 기존 수동 JSON 설정과는 별개입니다. 기존 이력의 원본/LFS 데이터 변환은 설치가 수행하지 않습니다.

## Mac과 빌드

Windows와 Mac은 같은 C# 저장·복원·규칙 코드를 사용하고 Mac GUI는 SwiftUI입니다. macOS 13 이상, Xcode Command Line Tools, .NET 9 SDK가 있는 Mac에서:

```bash
bash Tools/ResourceSplitter/Mac/Build.sh osx-arm64 # Apple Silicon
bash Tools/ResourceSplitter/Mac/Build.sh osx-x64   # Intel
```

결과는 `Tools/ResourceSplitter/bin/mac/<RID>/GitChunk.app`과 ZIP입니다. **Mac GUI의 실제 컴파일·실행은 미확인**이며 로컬 서명만 포함합니다. Windows에서는 Mac 공통 실행 파일 교차 빌드가 가능합니다.

GitHub Actions의 **Build Git Chunk**는 수동 실행만 지원합니다. 플랫폼별 빌드·통합 검증 뒤 아티팩트를 생성합니다.

Windows 빌드:

```powershell
dotnet publish Tools/ResourceSplitter/ResourceSplitter.csproj -c Release -r win-x64 -o publish/windows
```

소스 폴더와 csproj의 ResourceSplitter 이름은 경로 호환을 위해 유지합니다. 배포 이름은 GitChunk입니다. 상세 제한은 [INSTRUCTIONS.md](INSTRUCTIONS.md)를 참고하세요.

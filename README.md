# ResourceSplitter

50 MiB 초과 파일을 작은 복원 정보와 49 MiB 이하 조각으로 나누는 Windows x64 도구입니다. Git LFS나 별도 서버 없이 일반 Git에 저장합니다. 전체 용량을 줄이지는 않습니다.

## 세 메뉴는 같은 저장 형식을 사용합니다

- **1번 분할:** 원래 경로의 대용량 파일을 작은 복원 정보로 바꾸고 실제 데이터를 `.resourcesplitter/objects/`에 저장합니다. 직접 분할된 모습을 볼 수 있습니다.
- **2번 복원:** 복원 정보를 실제 원본으로 되돌립니다. 검증된 조각은 유지합니다. 기존 `.part`/manifest 형식도 복원합니다.
- **3번 Git 자동화 설치:** `git add`할 때 같은 형식으로 변환하고, 받을 때 원본으로 복원합니다. 자동화에서는 로컬 원본이 유지됩니다.

50 MiB 이하 파일과 `.meta`는 유지합니다. 수동 분할 상태에서는 에디터를 열지 말고 먼저 복원하세요. `.resourcesplitter/objects/`는 실제 데이터이므로 삭제하지 마세요.

Windows GUI EXE이므로 PowerShell에서 출력 확인과 작업 종료 대기가 필요한 명령에는 `| Out-Host`를 붙입니다. Git 훅과 GUI의 작업 실행은 종료까지 자동으로 기다립니다.

## 수동으로 확인하기

EXE를 더블클릭하면 Windows GUI가 열립니다. 폴더 선택 또는 폴더 드래그 후 **파일 분할 / 원본 복원 / Git 자동화** 카드를 선택하고 실행 버튼을 누르세요. 처리 중에는 진행 표시와 작업 기록을 확인할 수 있고, 로그 복사도 가능합니다.

처음 실행하면 **Git 자동화(권장)**가 미리 선택됩니다. 실행 버튼을 눌러야 설치됩니다.

```powershell
./ResourceSplitter.exe split ./TestProject | Out-Host
./ResourceSplitter.exe join ./TestProject | Out-Host
```

Git 저장소 안에서는 조각이 저장소 루트의 `.resourcesplitter/objects/`에 저장됩니다. Git 저장소가 아닌 폴더에서는 선택한 대상 폴더 안에 저장됩니다. 원래 경로의 복원 정보와 조각을 함께 보관하세요.

## Git 자동화 설치

GUI에서 Git 저장소 루트 폴더와 **Git 자동화** 카드를 선택하고 **자동화 설치**를 누르세요. 저장소/PC마다 한 번 설치합니다.

```powershell
./ResourceSplitter.exe git-install C:/path/to/project | Out-Host
# 최초 설정과 기존 추적 파일 반영
 git add --renormalize .
 git add .gitattributes .gitignore .resourcesplitter/ResourceSplitter.exe
 git commit -m "Enable automatic resource splitting"
 git push
```

이후 평소처럼 수정, git add, 커밋, 푸시, 풀하시면 됩니다. 새 PC에서 clone한 뒤에는 아래 명령을 한 번 실행합니다. 최초 복제된 복원 정보도 원본으로 돌아옵니다.

```powershell
./.resourcesplitter/ResourceSplitter.exe git-install | Out-Host
```

설치된 EXE를 업데이트하려면 새 EXE의 GUI에서 **Git 자동화**를 다시 설치하세요. 기존 다른 훅은 덮어쓰지 않고 설치를 중단합니다. 기존 이력의 대용량 원본/LFS 데이터 정리는 별도 작업입니다.

자세한 검증 및 제한은 [사용 지시사항](INSTRUCTIONS.md)을 참고하세요.

## Mac 버전 빌드

Windows와 Mac은 같은 C# 분할·복원·Git 코드를 사용합니다. Mac 화면은 `Tools/ResourceSplitter/Mac/MacApp.swift`의 SwiftUI로 구성하며 Git 자동화가 기본 선택입니다.

macOS 13 이상, Xcode Command Line Tools와 .NET 9 SDK가 있는 Mac에서 다음 명령으로 앱을 빌드합니다.

```bash
bash Tools/ResourceSplitter/Mac/Build.sh osx-arm64 # Apple Silicon
bash Tools/ResourceSplitter/Mac/Build.sh osx-x64   # Intel
```

결과는 `Tools/ResourceSplitter/bin/mac/<RID>/`의 `.app`과 ZIP입니다. 설치된 Git 도구는 `.resourcesplitter/ResourceSplitter-osx-arm64` 또는 `ResourceSplitter-osx-x64`입니다. 저장소를 다른 운영체제에서 사용할 때는 그 운영체제용 앱으로 자동화를 한 번 설치하세요.

`.github/workflows/build-desktop.yml`은 Windows, Apple Silicon, Intel 빌드와 Git 왕복 검증을 구성합니다. 현재 Windows에서 두 Mac 공통 실행 파일의 교차 빌드까지 확인했습니다. **Mac GUI 컴파일·실행과 CI 실제 실행은 미확인**입니다. Mac 패키지는 로컬 서명이며 Developer ID 서명·공증은 포함하지 않습니다.

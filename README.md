# ResourceSplitter

프로젝트 파일을 조각으로 분할하고 원래 경로에 복원하는 Windows 도구입니다.

## 목적

Git LFS 없이 Unity·Unreal 프로젝트 등의 대용량 리소스를 일반 Git에 보관하기 위해 만들었습니다. **50 MiB 초과 파일만** 원본과 같은 폴더에 기본 49 MiB 단위의 조각을 저장하고 원본을 삭제하며, 필요할 때 원래 파일로 복원합니다. 50 MiB 이하 파일은 유지하며, 분할 대상의 Unity `.meta`는 크기와 관계없이 별도 조각으로 보관해 함께 복원합니다. 분할해도 전체 용량은 줄어들지 않습니다.

## 사용 방법

### Git 자동 분할·복원 (권장)

`ResourceSplitter.exe`를 실행하고 **Git 프로젝트 루트 경로**를 선택한 다음 **3번**을 누르세요. 프로젝트마다, 사용하는 PC마다 최초 한 번 설치합니다. 터미널에서는 다음처럼 설치할 수 있습니다.

```powershell
./ResourceSplitter.exe git-install C:/path/to/project
```

처음 설정을 커밋할 때 다음 명령을 실행하세요. 이미 추적 중인 파일도 새 저장 형식으로 반영됩니다.

```powershell
git add --renormalize .
git add .gitattributes .gitignore .resourcesplitter/ResourceSplitter.exe
git commit -m "Enable automatic resource splitting"
git push
```

그 이후에는 **평소처럼 파일을 수정하고 `git add`, 커밋, 푸시, 풀하시면 됩니다.** 50 MiB 초과 파일은 Git에 넣을 때 자동 분할되며, 로컬 원본은 유지됩니다. 풀·브랜치 전환 시 원본을 자동 복원합니다. `.meta`는 일반 파일로 함께 추적하므로 GUID가 유지됩니다. 조각은 일반 Git 데이터이며 Git LFS나 별도 서버를 사용하지 않습니다.

다른 PC에서 복제한 뒤에는 저장소의 `.resourcesplitter/ResourceSplitter.exe`를 실행해 3번을 선택하거나 아래 명령을 한 번 실행하세요. 처음 복제된 포인터 파일을 설치 과정에서 원본으로 복원합니다.

```powershell
./.resourcesplitter/ResourceSplitter.exe git-install
```

기존 수동 분할 상태라면 **먼저 2번으로 복원한 뒤** 설치하세요. 기존 훅이 있으면 설치를 중단하며 덮어쓰지 않습니다. 자세한 동작과 제한은 [사용 지시사항](INSTRUCTIONS.md)을 참고하세요.

### 수동 분할·복원

1. Windows x64용 `ResourceSplitter.exe`를 받습니다. .NET을 별도로 설치할 필요가 없습니다.
2. `ResourceSplitter.exe`를 실행하고 대상 프로젝트 경로를 입력합니다. Enter를 누르면 현재 작업 폴더를 사용합니다. 터미널에서는 실행 명령을 입력한 폴더가 기준입니다.
3. `1`은 분할 후 원본 삭제, `2`는 원본 복원입니다. 분할 전 백업하고 에디터를 닫아 주세요.

실행에는 `ResourceSplitter.exe` 하나만 필요합니다.

**사용 전 [사용 지시사항](INSTRUCTIONS.md)을 확인하세요.** 분할 대상 설정과 복원·Git 저장 시 주의사항을 안내합니다.

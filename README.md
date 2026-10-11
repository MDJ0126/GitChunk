# ResourceSplitter

50 MiB 초과 파일을 작은 복원 정보와 49 MiB 이하 조각으로 나누는 Windows x64 도구입니다. Git LFS나 별도 서버 없이 일반 Git에 저장합니다. 전체 용량을 줄이지는 않습니다.

## 세 메뉴는 같은 저장 형식을 사용합니다

- **1번 분할:** 원래 경로의 대용량 파일을 작은 복원 정보로 바꾸고 실제 데이터를 `.resourcesplitter/objects/`에 저장합니다. 직접 분할된 모습을 볼 수 있습니다.
- **2번 복원:** 복원 정보를 실제 원본으로 되돌립니다. 검증된 조각은 유지합니다. 기존 `.part`/manifest 형식도 복원합니다.
- **3번 Git 자동화 설치:** `git add`할 때 같은 형식으로 변환하고, 받을 때 원본으로 복원합니다. 자동화에서는 로컬 원본이 유지됩니다.

50 MiB 이하 파일과 `.meta`는 유지합니다. 수동 분할 상태에서는 에디터를 열지 말고 먼저 복원하세요. `.resourcesplitter/objects/`는 실제 데이터이므로 삭제하지 마세요.

## 수동으로 확인하기

EXE를 실행하고 대상 경로를 입력한 다음 1번/2번을 선택합니다. 터미널에서도 가능합니다.

```powershell
./ResourceSplitter.exe split ./TestProject
./ResourceSplitter.exe join ./TestProject
```

Git 저장소 안에서는 조각이 저장소 루트의 `.resourcesplitter/objects/`에 저장됩니다. Git 저장소가 아닌 폴더에서는 선택한 대상 폴더 안에 저장됩니다. 원래 경로의 복원 정보와 조각을 함께 보관하세요.

## Git 자동화 설치

EXE에서 Git 저장소 루트 경로를 선택하고 3번을 누르세요. 저장소/PC마다 한 번 설치합니다.

```powershell
./ResourceSplitter.exe git-install C:/path/to/project
# 최초 설정과 기존 추적 파일 반영
 git add --renormalize .
 git add .gitattributes .gitignore .resourcesplitter/ResourceSplitter.exe
 git commit -m "Enable automatic resource splitting"
 git push
```

이후 평소처럼 수정, git add, 커밋, 푸시, 풀하시면 됩니다. 새 PC에서 clone한 뒤에는 아래 명령을 한 번 실행합니다. 최초 복제된 복원 정보도 원본으로 돌아옵니다.

```powershell
./.resourcesplitter/ResourceSplitter.exe git-install
```

설치된 EXE를 업데이트하려면 새 EXE에서 3번을 다시 실행하세요. 기존 다른 훅은 덮어쓰지 않고 설치를 중단합니다. 기존 이력의 대용량 원본/LFS 데이터 정리는 별도 작업입니다.

자세한 검증 및 제한은 [사용 지시사항](INSTRUCTIONS.md)을 참고하세요.
# ResourceSplitter

프로젝트 파일을 조각으로 분할하고 원래 경로에 복원하는 Windows 도구입니다.

## 목적

Unity·Unreal 프로젝트 등의 대용량 리소스를 작은 조각으로 나누어 일반 Git에 보관하고, 다른 환경에서 원래 파일과 경로로 복원하기 위한 도구입니다. 기본 49 MiB 단위로 분할하며, 압축으로 전체 용량을 줄이는 도구는 아닙니다.

## 사용 방법

1. Windows용 .NET 9 Runtime을 설치합니다.
2. `ResourceSplitter.exe`를 실행하고 대상 프로젝트 경로를 입력합니다. Enter를 누르면 실행 파일이 있는 폴더를 사용합니다.
3. `1`은 분할, `2`는 복원입니다.

다른 폴더로 옮길 때는 `.exe`, `.dll`, `.runtimeconfig.json`, `.deps.json` 네 파일을 함께 복사하세요.

**사용 전 [사용 지시사항](INSTRUCTIONS.md)을 확인하세요.** 분할 대상 설정과 복원·Git 저장 시 주의사항을 안내합니다.

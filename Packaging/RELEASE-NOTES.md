# Git Chunk 1.0.0

Keep large files in Git and work as usual.

Install Git Chunk and set it up once for your project. Then edit your original files and commit, push, and pull as usual. Splitting large files for storage and restoring the originals happens automatically.

## Features

- Splits files over 50 MiB into chunks of up to 49 MiB and stores them in the same Git repository as your code. No separate LFS storage service is needed.
- Keeps original files in your working directory. No manual splitting or reassembly is needed.
- Lets you select a project and configure file exclusions in the app.
- Converts current files in an existing Git LFS project. Past LFS history and server data are preserved.
- Provides a Windows installer and portable ZIP, macOS Apple Silicon and Intel packages, and Scoop and Homebrew installation. Git is required; the .NET SDK is not.

## Install and get started

On Windows, download and run **GitChunk-Setup-win-x64.exe**. On macOS, choose **GitChunk-osx-arm64.zip** for Apple Silicon or **GitChunk-osx-x64.zip** for Intel, then move the app to Applications.

Select your project folder in the Git Chunk app and click **Install Git Chunk**, or run `git chunk install` from the project folder. Commit the initial configuration, then use Git as usual. Setup is also required on new computers and each teammate's computer.

[English guide](https://github.com/MDJ0126/GitChunk/blob/main/README.md) · [한국어 사용 안내](https://github.com/MDJ0126/GitChunk/blob/main/README.ko.md)

## Release notes

This official release restarts versioning at **v1.0.0** after the earlier test releases. If you installed a 1.1.x or 1.2.x test version, reinstall using the new installer instead of automatic updates. Keep your existing project's original files and chunk data, then apply the new program to the project again.

Chunk data counts toward ordinary Git repository storage, and your hosting provider's limits still apply. Windows and macOS binaries are not code-signed or Apple-notarized, so your operating system may display a confirmation when you run them. Use **SHA256SUMS** to verify downloaded files.

---

## 한국어

큰 파일도 Git으로 관리하고, 작업은 평소처럼 하세요.

Git Chunk를 설치하고 프로젝트에 한 번 적용하면 원본 파일을 편집하고 평소처럼 커밋·푸시·풀하시면 됩니다. 큰 파일을 나누어 저장하고 원본으로 복원하는 작업은 자동으로 처리합니다.

## 주요 기능

- 50 MiB 초과 파일을 최대 49 MiB 조각으로 나누어 코드와 같은 Git 저장소에 저장합니다. 별도 LFS 저장 서비스는 필요 없습니다.
- 작업 폴더에서는 원본 파일을 그대로 사용합니다. 파일을 직접 나누거나 합칠 필요가 없습니다.
- 앱에서 프로젝트를 선택해 적용하고, 나누지 않을 파일을 지정할 수 있습니다.
- 기존 Git LFS 프로젝트의 현재 파일을 전환할 수 있습니다. 과거 LFS 이력과 서버 데이터는 유지됩니다.
- Windows 설치 파일과 포터블 ZIP, macOS Apple Silicon·Intel 패키지, Scoop·Homebrew 설치를 제공합니다. Git은 필요하지만 .NET SDK는 필요 없습니다.

## 설치 및 시작

Windows는 **GitChunk-Setup-win-x64.exe**를 내려받아 실행하세요. macOS는 Apple Silicon이면 **GitChunk-osx-arm64.zip**, Intel이면 **GitChunk-osx-x64.zip**을 선택해 앱을 Applications에 옮기세요.

Git Chunk 앱에서 프로젝트 폴더를 선택하고 **Install Git Chunk**를 누르거나, 프로젝트 폴더에서 `git chunk install`을 실행하세요. 처음 적용한 설정을 커밋한 뒤에는 평소처럼 Git을 사용하시면 됩니다. 새 PC와 팀원의 PC에서도 설치·프로젝트 적용이 필요합니다.

[한국어 사용 안내](https://github.com/MDJ0126/GitChunk/blob/main/README.ko.md) · [English guide](https://github.com/MDJ0126/GitChunk/blob/main/README.md)

## 배포 안내

이번 릴리스는 이전 시험 배포를 정리하고 **v1.0.0**으로 다시 시작하는 공식 배포입니다. 이전 1.1.x·1.2.x 시험 버전을 설치하셨다면 자동 업데이트 대신 새 설치 파일로 재설치해 주세요. 기존 프로젝트의 원본과 조각 데이터는 유지하고 새 프로그램을 프로젝트에 다시 적용하세요.

조각 데이터는 일반 Git 저장 용량에 포함되며 호스팅 서비스의 제한이 적용됩니다. Windows·macOS 배포 파일은 코드 서명 및 Apple 공증이 되어 있지 않아 실행 시 운영체제의 확인 안내가 나타날 수 있습니다. **SHA256SUMS**로 다운로드 파일의 무결성을 확인할 수 있습니다.

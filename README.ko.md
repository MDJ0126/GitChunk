# Git Chunk

[English](README.md)

대용량 파일을 Git에 자동 분할 저장하고, 로컬에는 원본을 유지합니다. Git만 필요하며 .NET SDK는 필요 없습니다.

## 설치

**Windows** — [설치 파일 다운로드](https://github.com/MDJ0126/GitChunk/releases/latest), 또는 Scoop으로:

```powershell
scoop bucket add gitchunk https://github.com/MDJ0126/GitChunk.git
scoop install gitchunk/git-chunk
```

**Mac** — Homebrew로:

```bash
brew tap MDJ0126/gitchunk https://github.com/MDJ0126/GitChunk
brew install --cask MDJ0126/gitchunk/git-chunk
```

## 사용

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

이후 평소처럼 Git을 사용하시면 됩니다. 앱에서도 프로젝트를 선택하고 **Install Git Chunk**를 눌러 설정할 수 있습니다.

분할 대상은 `.gitchunk/rules.txt` 또는 앱에서 설정합니다. 대상 중 **50 MiB 초과** 파일을 최대 **49 MiB** 조각으로 저장합니다. `.gitchunk/objects`는 삭제하지 마세요.

[배포 상세](Packaging/README.md) · [기술 설명](INSTRUCTIONS.md)

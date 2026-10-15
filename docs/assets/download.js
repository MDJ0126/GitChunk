(() => {
  const platform = navigator.userAgentData?.platform || navigator.platform || '';
  const isMac = /mac/i.test(platform) && !(navigator.maxTouchPoints > 1);
  if (!isMac) return;
  const actions = document.querySelector('.download-actions');
  if (!actions) return;
  const ko = document.documentElement.lang === 'ko';
  const base = 'https://github.com/MDJ0126/GitChunk/releases/latest/download/';
  const button = actions.querySelector('.download-button');
  // Browsers do not reliably expose Mac CPU architecture. Let the user choose.
  button.href = base + 'GitChunk-osx-arm64.zip';
  button.replaceChildren();
  const label = document.createTextNode(ko ? 'macOS용 다운로드' : 'Download for macOS');
  const chip = document.createElement('span');
  chip.textContent = 'Apple Silicon';
  button.append(label, chip);
  const alternatives = actions.querySelector('.download-alternatives');
  alternatives.replaceChildren();
  const intel = document.createElement('a');
  intel.href = base + 'GitChunk-osx-x64.zip';
  intel.textContent = ko ? 'Intel Mac용 다운로드' : 'Download for Intel Mac';
  const separator = document.createElement('span');
  separator.setAttribute('aria-hidden', 'true');
  separator.textContent = '·';
  const windows = document.createElement('a');
  windows.href = base + 'GitChunk-Setup-win-x64.exe';
  windows.textContent = 'Windows';
  alternatives.append(intel, separator, windows);
})();

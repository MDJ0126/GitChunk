(() => {
  const platform = navigator.userAgentData?.platform || navigator.platform || '';
  const isMac = /mac/i.test(platform) && !(navigator.maxTouchPoints > 1);
  const isLinux = /linux/i.test(platform) && !/android/i.test(navigator.userAgent);
  if (!isMac && !isLinux) return;
  const actions = document.querySelector('.download-actions');
  if (!actions) return;
  const ko = document.documentElement.lang === 'ko';
  const base = 'https://github.com/MDJ0126/GitChunk/releases/latest/download/';
  const button = actions.querySelector('.download-button');
  // Browsers do not reliably expose Mac CPU architecture. Let the user choose.
  // Linux ARM is not offered by this release; keep explicit platform choices there.
  if (isLinux && /aarch64|arm/i.test(navigator.userAgent)) return;
  button.href = base + (isLinux ? 'GitChunk-linux-x64.zip' : 'GitChunk-osx-arm64.zip');
  button.replaceChildren();
  const label = document.createTextNode(isLinux ? (ko ? 'Linux용 다운로드' : 'Download for Linux') : (ko ? 'macOS용 다운로드' : 'Download for macOS'));
  const chip = document.createElement('span');
  chip.textContent = isLinux ? 'x64' : 'Apple Silicon';
  button.append(label, chip);
  const alternatives = actions.querySelector('.download-alternatives');
  alternatives.replaceChildren();
  const intel = document.createElement('a');
  intel.href = base + (isLinux ? 'GitChunk-osx-arm64.zip' : 'GitChunk-osx-x64.zip');
  intel.textContent = isLinux ? 'macOS (Apple Silicon)' : (ko ? 'Intel Mac용 다운로드' : 'Download for Intel Mac');
  const separator = document.createElement('span');
  separator.setAttribute('aria-hidden', 'true');
  separator.textContent = '·';
  const windows = document.createElement('a');
  windows.href = base + 'GitChunk-Setup-win-x64.exe';
  windows.textContent = 'Windows';
  alternatives.append(intel, separator, windows);
  const extraSeparator = separator.cloneNode(true);
  const extra = document.createElement('a');
  extra.href = base + (isLinux ? 'GitChunk-osx-x64.zip' : 'GitChunk-linux-x64.zip');
  extra.textContent = isLinux ? 'Intel Mac' : 'Linux x64';
  alternatives.append(extraSeparator, extra);
})();

(() => {
  const root = document.documentElement;
  const preference = window.matchMedia('(prefers-color-scheme: dark)');
  let saved;
  try { saved = localStorage.getItem('gitchunk-theme'); } catch {}
  const setTheme = theme => {
    root.dataset.theme = theme;
    const button = document.querySelector('.theme-toggle');
    if (!button) return;
    const dark = theme === 'dark';
    const ko = root.lang === 'ko';
    const label = ko ? (dark ? '라이트 모드로 전환' : '다크 모드로 전환') : (dark ? 'Switch to light mode' : 'Switch to dark mode');
    button.setAttribute('aria-label', label);
    button.setAttribute('title', label);
    button.setAttribute('aria-pressed', String(dark));
  };
  setTheme(saved === 'dark' || saved === 'light' ? saved : (preference.matches ? 'dark' : 'light'));
  document.addEventListener('DOMContentLoaded', () => {
    setTheme(root.dataset.theme);
    document.querySelector('.theme-toggle')?.addEventListener('click', () => {
      saved = root.dataset.theme === 'dark' ? 'light' : 'dark';
      setTheme(saved);
      try { localStorage.setItem('gitchunk-theme', saved); } catch {}
    });
  });
  preference.addEventListener('change', event => {
    if (saved !== 'dark' && saved !== 'light') setTheme(event.matches ? 'dark' : 'light');
  });
})();

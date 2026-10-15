const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const docs = path.join(root, 'docs');
const repo = 'https://github.com/MDJ0126/GitChunk/blob/main/';
const escape = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
function target(href) {
  if (href === 'guide.md') return 'guide.html';
  if (href === 'guide.ko.md') return 'guide.ko.html';
  if (href.startsWith('../')) return repo + href.slice(3);
  return href;
}
function inline(text) {
  const tokens = [];
  let result = text.replace(/`([^`]+)`/g, (_, code) => `\u0000${tokens.push('<code>' + escape(code) + '</code>') - 1}\u0000`);
  result = escape(result).replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, label, href) => '<a href="' + escape(target(href.replace(/&amp;/g, '&'))) + '">' + label + '</a>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  return result.replace(/\u0000(\d+)\u0000/g, (_, n) => tokens[n]);
}
function render(markdown) {
  const output = [], toc = [], ids = new Map();
  const lines = markdown.replace(/\r/g, '').split('\n');
  let paragraph = [], list = null;
  function flush() { if (paragraph.length) output.push('<p>' + inline(paragraph.join(' ')) + '</p>'); paragraph = []; }
  function endList() { if (list) output.push('</' + list + '>'); list = null; }
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (line.startsWith('```')) {
      flush(); endList(); const language = line.slice(3).trim(); const code = [];
      while (++i < lines.length && !lines[i].startsWith('```')) code.push(lines[i]);
      output.push('<pre><code class="language-' + escape(language) + '">' + escape(code.join('\n')) + '</code></pre>'); continue;
    }
    const heading = /^(#{1,6}) (.+)$/.exec(line);
    if (heading) {
      flush(); endList(); const level = heading[1].length;
      if (level === 1) continue;
      const slug = heading[2].toLowerCase().replace(/[^\p{L}\p{N} -]/gu, '').trim().replace(/ +/g, '-');
      const count = ids.get(slug) || 0; ids.set(slug, count + 1); const id = slug + (count ? '-' + count : '');
      output.push(`<h${level} id="${id}">${inline(heading[2])}</h${level}>`);
      if (level === 2) toc.push(`<a href="#${id}">${inline(heading[2])}</a>`); continue;
    }
    const item = /^(?:(\d+)\. |([-*]) )(.+)$/.exec(line);
    if (item) {
      flush(); const type = item[1] ? 'ol' : 'ul';
      if (list !== type) { endList(); list = type; output.push('<' + type + '>'); }
      output.push('<li>' + inline(item[3]) + '</li>'); continue;
    }
    if (!line.trim()) { flush(); endList(); continue; }
    endList();
    // Language switching lives in the site header, not in the guide body.
    if (/^\[(English|한국어)\]\(guide/.test(line)) continue;
    paragraph.push(line.trim());
  }
  flush(); endList(); return { body: output.join('\n'), toc: toc.join('\n') };
}
for (const ko of [false, true]) {
  const suffix = ko ? '.ko' : '';
  const source = fs.readFileSync(path.join(docs, 'guide' + suffix + '.md'), 'utf8');
  const { body, toc } = render(source);
  const home = 'index' + suffix + '.html';
  const other = 'guide' + (ko ? '' : '.ko') + '.html';
  const template = fs.readFileSync(path.join(docs, home), 'utf8');
  const header = template.match(/<header>.*?<\/header>/s)[0]
    .replace('href="#"', `href="${home}"`)
    .replace('href="#getting-started"', `href="${home}#getting-started"`)
    .replace(`href="${ko ? 'index.html' : 'index.ko.html'}"`, `href="${other}"`);
  const footer = template.match(/<footer>.*?<\/footer>/s)[0];
  const html = `<!doctype html><html lang="${ko ? 'ko' : 'en'}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Git Chunk — ${ko ? '사용 가이드' : 'User guide'}</title><meta name="description" content="${ko ? 'Git Chunk 설치, 사용, 업데이트, LFS 전환 및 제거 안내' : 'Install, use, update, migrate from LFS, and uninstall Git Chunk'}"><link rel="icon" href="assets/git-chunk-symbol.svg" type="image/svg+xml"><script src="assets/theme.js"></script><link rel="stylesheet" href="assets/site.css"></head><body>${header}<main class="wrap guide-layout"><aside class="guide-toc" aria-label="${ko ? '목차' : 'Contents'}"><strong>${ko ? '사용 가이드' : 'User guide'}</strong>${toc}</aside><article class="guide-article"><span class="eyebrow">Git Chunk / Documentation</span><h1>${ko ? '사용 가이드' : 'User guide'}</h1>${body}</article></main>${footer}</body></html>`;
  fs.writeFileSync(path.join(docs, 'guide' + suffix + '.html'), html);
  console.log('Built guide' + suffix + '.html from Markdown');
}

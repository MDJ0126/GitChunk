// Shared social metadata for generated guides and static landing pages.
module.exports = function socialMeta(file, title, description, ko) {
  const esc = value => value.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
  const base = 'https://mdj0126.github.io/GitChunk/';
  const url = base + (file === 'index.html' ? '' : file);
  const image = base + 'assets/git-chunk-og.png';
  const tags = {
    'og:type': 'website', 'og:site_name': 'Git Chunk', 'og:title': title,
    'og:description': description, 'og:url': url, 'og:locale': ko ? 'ko_KR' : 'en_US',
    'og:image': image, 'og:image:type': 'image/png', 'og:image:width': '1733', 'og:image:height': '908',
    'og:image:alt': 'Git Chunk — Large files. One Git repository. A blue chunk logo and a file split into pieces.'
  };
  return '<link rel="canonical" href="' + url + '">' + Object.entries(tags).map(([key,value]) => `<meta property="${key}" content="${esc(value)}">`).join('') +
    `<meta name="twitter:card" content="summary_large_image"><meta name="twitter:title" content="${esc(title)}"><meta name="twitter:description" content="${esc(description)}"><meta name="twitter:image" content="${image}"><meta name="twitter:image:alt" content="${esc(tags['og:image:alt'])}">`;
};

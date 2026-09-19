const frame = document.querySelector('iframe');
const buttons = [...document.querySelectorAll('button[data-palette]')];
let selected = ['navy', 'forest', 'wine'].includes(location.hash.slice(1)) ? location.hash.slice(1) : 'navy';
function applyPalette() {
  if (frame.contentDocument?.documentElement) frame.contentDocument.documentElement.dataset.palette = selected;
  for (const button of buttons) button.setAttribute('aria-pressed', String(button.dataset.palette === selected));
}
for (const button of buttons) button.addEventListener('click', () => {
  selected = button.dataset.palette;
  history.replaceState(null, '', '#' + selected);
  applyPalette();
});
frame.addEventListener('load', () => {
  applyPalette();
  new ResizeObserver(() => {
    frame.style.height = (frame.contentDocument.body.scrollHeight + 24) + 'px';
  }).observe(frame.contentDocument.body);
});

// ====== CONFIG ======
const TEMPLATE_FOLDER = 'templates/'; // path to PNG outlines
const TEMPLATE_LIST = [ 'seahorse.jpg', 'cat.png', 'shark.png' ];
const PALETTE_COLOURS = [
  '#FF0000','#FFA500','#FFFF00','#008000','#0000FF',
  '#4B0082','#EE82EE','#000000','#FFFFFF'
];
// ======================

const galleryEl = document.getElementById('gallery');
const drawingArea = document.getElementById('drawing-area');
const paletteEl = document.getElementById('colour-palette');
const clearBtn = document.getElementById('clear-btn');
const saveBtn = document.getElementById('save-btn');
const brushSizeInput = document.getElementById('brush-size');

let currentColour = '#FF0000';
let currentTemplate = null;
let canvas, ctx, outlineImg;
let isDrawing = false;

// 1. Build thumbnail gallery
TEMPLATE_LIST.forEach(filename => {
  const thumb = document.createElement('img');
  thumb.src = TEMPLATE_FOLDER + filename;
  thumb.alt = filename;
  thumb.addEventListener('click', () => selectTemplate(filename, thumb));
  galleryEl.appendChild(thumb);
});

// 2. Build colour palette
PALETTE_COLOURS.forEach(col => {
  const sw = document.createElement('div');
  sw.classList.add('swatch');
  sw.style.backgroundColor = col;
  sw.addEventListener('click', () => {
    currentColour = col;
    document.querySelectorAll('.swatch').forEach(s => s.style.outline = '');
    sw.style.outline = '2px solid #333';
  });
  paletteEl.appendChild(sw);
});
document.querySelector('.swatch').style.outline = '2px solid #333';

// 3. Select template: create overlay <canvas> + <img>
function selectTemplate(filename, thumbEl) {
  document.querySelectorAll('#gallery img').forEach(i => i.classList.remove('selected'));
  thumbEl.classList.add('selected');

  drawingArea.innerHTML = ''; // clear any existing

  // Create outline image
  outlineImg = document.createElement('img');
  outlineImg.id = 'outline-img';
  outlineImg.src = TEMPLATE_FOLDER + filename;
  outlineImg.onload = setupCanvas;
  drawingArea.appendChild(outlineImg);
}

function setupCanvas() {
  // Create canvas same size as outline
  canvas = document.createElement('canvas');
  canvas.width = outlineImg.naturalWidth;
  canvas.height = outlineImg.naturalHeight;
  drawingArea.appendChild(canvas);

  // Position absolute over the image
  outlineImg.style.width = canvas.width + 'px';
  outlineImg.style.height = canvas.height + 'px';
  canvas.style.position = 'absolute';
  canvas.style.top = '0';
  canvas.style.left = '0';

  ctx = canvas.getContext('2d');
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  // Mouse / touch events for brush
  canvas.addEventListener('mousedown', e => {
    isDrawing = true;
    draw(e.offsetX, e.offsetY);
  });
  canvas.addEventListener('mousemove', e => {
    if (!isDrawing) return;
    draw(e.offsetX, e.offsetY);
  });
  window.addEventListener('mouseup', () => isDrawing = false);

  // For mobile / touch
  canvas.addEventListener('touchstart', e => {
    e.preventDefault();
    isDrawing = true;
    const touch = e.touches[0];
    const rect = canvas.getBoundingClientRect();
    const x = touch.clientX - rect.left;
    const y = touch.clientY - rect.top;
    draw(x, y);
  });
  canvas.addEventListener('touchmove', e => {
    if (!isDrawing) return;
    e.preventDefault();
    const touch = e.touches[0];
    const rect = canvas.getBoundingClientRect();
    draw(touch.clientX - rect.left, touch.clientY - rect.top);
  });
  window.addEventListener('touchend', () => isDrawing = false);
}

function draw(x, y) {
  const size = parseInt(brushSizeInput.value, 10);
  ctx.fillStyle = currentColour;
  ctx.beginPath();
  ctx.arc(x, y, size / 2, 0, Math.PI * 2);
  ctx.fill();
}

// 4. Clear: reset canvas
clearBtn.addEventListener('click', () => {
  if (!canvas) return;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, canvas.width, canvas.height);
});

// 5. Save: composite outline + canvas into one PNG
saveBtn.addEventListener('click', () => {
  if (!canvas || !outlineImg) return;

  const compositeCanvas = document.createElement('canvas');
  compositeCanvas.width = canvas.width;
  compositeCanvas.height = canvas.height;
  const cctx = compositeCanvas.getContext('2d');

  // Draw the canvas (user’s paint) first
  cctx.drawImage(canvas, 0, 0);
  // Then draw the outline on top (so black lines appear over colours)
  cctx.drawImage(outlineImg, 0, 0, canvas.width, canvas.height);

  // Download as PNG
  const dataURL = compositeCanvas.toDataURL('image/png');
  const link = document.createElement('a');
  link.href = dataURL;
  link.download = 'coloured_page.png';
  link.click();
});

// Auto-select first template
window.addEventListener('DOMContentLoaded', () => {
  if (TEMPLATE_LIST.length) {
    const firstThumb = galleryEl.querySelector('img');
    selectTemplate(TEMPLATE_LIST[0], firstThumb);
  }
});

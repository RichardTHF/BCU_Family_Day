// ========== CONFIG ========== 
const TEMPLATE_FOLDER = 'templates/'; // adjust path if needed
const TEMPLATE_LIST = [
  'butterfly.svg',
  'castle.svg',
  'dragon.svg',
  // …etc—list all SVG filenames here
];
const PALETTE_COLOURS = [
  '#FF0000', '#FFA500', '#FFFF00', '#008000', '#0000FF',
  '#4B0082', '#EE82EE', '#000000', '#FFFFFF'
];
// =============================

const galleryEl = document.getElementById('gallery');
const drawingArea = document.getElementById('drawing-area');
const colourPaletteEl = document.getElementById('colour-palette');
const clearBtn = document.getElementById('clear-btn');
const saveBtn = document.getElementById('save-btn');

let currentColour = '#FF0000';
let currentSVG = null; // reference to loaded SVG document

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
    // visually indicate selection
    document.querySelectorAll('.swatch').forEach(s => s.style.outline = '');
    sw.style.outline = '2px solid #333';
  });
  colourPaletteEl.appendChild(sw);
});
// Default select first swatch
document.querySelector('.swatch').style.outline = '2px solid #333';

// 3. Load an SVG template into #drawing-area
function selectTemplate(filename, thumbElement) {
  // highlight selected thumbnail
  document.querySelectorAll('#gallery img').forEach(img => img.classList.remove('selected'));
  thumbElement.classList.add('selected');

  fetch(TEMPLATE_FOLDER + filename)
    .then(resp => resp.text())
    .then(svgText => {
      drawingArea.innerHTML = svgText;
      currentSVG = drawingArea.querySelector('svg');

      // Remove any inline fill attributes so everything starts “white”
      currentSVG.querySelectorAll('[fill]').forEach(el => {
        el.setAttribute('data-original-fill', el.getAttribute('fill'));
        el.removeAttribute('fill');
      });

      // Attach click handler to every <path>, <polygon>, <circle>, <rect>, etc.
      // You can pick whichever shapes exist in your SVG. Here, we grab everything.
      currentSVG.querySelectorAll('*').forEach(shape => {
        if (shape.tagName !== 'svg' && shape.tagName !== 'g') {
          shape.style.cursor = 'pointer';
          shape.addEventListener('click', e => {
            e.stopPropagation(); 
            shape.setAttribute('fill', currentColour);
          });
        }
      });
    })
    .catch(err => console.error('Error loading SVG:', err));
}

// 4. Clear all fills (reset to white / no fill)
clearBtn.addEventListener('click', () => {
  if (!currentSVG) return;
  currentSVG.querySelectorAll('[fill]').forEach(el => {
    el.removeAttribute('fill');
  });
});

// 5. Save as PNG: render the SVG onto a temporary canvas, then download
saveBtn.addEventListener('click', () => {
  if (!currentSVG) return;
  // Serialize the SVG with current fills
  const serializer = new XMLSerializer();
  const svgStr = serializer.serializeToString(currentSVG);

  // Create image blob from SVG string
  const img = new Image();
  const svgBlob = new Blob([svgStr], { type: 'image/svg+xml;charset=utf-8' });
  const url = URL.createObjectURL(svgBlob);

  img.onload = () => {
    const canvas = document.createElement('canvas');
    canvas.width = img.width;
    canvas.height = img.height;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(img, 0, 0);
    URL.revokeObjectURL(url);

    // Trigger download
    const pngURL = canvas.toDataURL('image/png');
    const link = document.createElement('a');
    link.href = pngURL;
    link.download = 'my_coloured_image.png';
    link.click();
  };
  img.src = url;
});

// Optionally, auto-select the first template on load:
window.addEventListener('DOMContentLoaded', () => {
  if (TEMPLATE_LIST.length) {
    selectTemplate(TEMPLATE_LIST[0], galleryEl.querySelector('img'));
  }
});

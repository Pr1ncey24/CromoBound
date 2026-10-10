// Scales the 1440x900 board to fit the window (spec 5.1): the frame gets --board-scale, and the board inside is scaled by it.
window.cromoBoard = {
  fit(frame) {
    if (!frame || frame._cromoFit) return;
    const apply = () => {
      const top = frame.getBoundingClientRect().top;
      const scale = Math.min(window.innerWidth / 1440, (window.innerHeight - top) / 900);
      frame.style.setProperty('--board-scale', String(Math.max(0.3, scale)));
    };
    frame._cromoFit = apply;
    window.addEventListener('resize', apply);
    apply();
  },
  release(frame) {
    if (!frame || !frame._cromoFit) return;
    window.removeEventListener('resize', frame._cromoFit);
    frame._cromoFit = null;
  },
};

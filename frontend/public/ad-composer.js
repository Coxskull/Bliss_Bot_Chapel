"use strict";
// Original deterministic artwork from a confirmed advertiser brief. No images,
// remote resources, provider APIs, fonts, external templates or model calls.
(function () {
  const FALLBACK = ["#22133D", "#E4B96C"];
  const clean = (value, limit) => String(value ?? "").replace(/[\u0000-\u001f\u007f]/g, " ").trim().slice(0, limit);
  function colors(palette) {
    const matches = String(palette ?? "").match(/#[0-9a-fA-F]{6}\b/g) || [];
    return [matches[0] || FALLBACK[0], matches[1] || FALLBACK[1]];
  }
  function roundRect(ctx, x, y, w, h, r, color) {
    const radius = Math.min(r, w / 2, h / 2);
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.roundRect(x, y, w, h, radius);
    ctx.fill();
  }
  function drawWrapped(ctx, value, x, y, width, options) {
    const { fontWeight = 700, maxLines = 3, initialSize = 72, minSize = 30, lineHeight = 1.18, color = "#FFFFFF" } = options;
    const input = clean(value, 500).replace(/\s+/g, " ") || "Your next story begins here";
    function linesFor(size) {
      ctx.font = `${fontWeight} ${size}px system-ui, sans-serif`;
      const tokens = input.split(" ");
      const lines = [];
      let line = "";
      for (const token of tokens) {
        const attempt = line ? line + " " + token : token;
        if (line && ctx.measureText(attempt).width > width) { lines.push(line); line = token; }
        else line = attempt;
      }
      if (line) lines.push(line);
      return lines;
    }
    let size = initialSize, lines = linesFor(size);
    while (lines.length > maxLines && size > minSize) {
      size -= 2;
      lines = linesFor(size);
    }
    if (lines.length > maxLines) {
      lines = lines.slice(0, maxLines);
      let last = lines[maxLines - 1];
      ctx.font = `${fontWeight} ${size}px system-ui, sans-serif`;
      while (last.length > 1 && ctx.measureText(last + "…").width > width) last = last.slice(0, -1);
      lines[maxLines - 1] = last.trimEnd() + "…";
    }
    ctx.fillStyle = color;
    ctx.font = `${fontWeight} ${size}px system-ui, sans-serif`;
    ctx.textBaseline = "alphabetic";
    lines.forEach((line, index) => ctx.fillText(line, x, y + index * size * lineHeight, width));
    return y + (lines.length - 1) * size * lineHeight;
  }
  function render(input) {
    const square = input?.format === "square";
    const width = square ? 1080 : 1280, height = square ? 1080 : 720;
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext("2d", { alpha: false });
    if (!ctx) throw new Error("Canvas artwork is not available in this browser.");
    const [brandColor, accent] = colors(input?.colors);
    const brand = clean(input?.brandName, 120) || "YOUR BRAND";
    const headline = clean(input?.headline, 500);
    const callToAction = clean(input?.cta, 120) || "Discover more";
    const market = clean(input?.market, 80);
    const margin = square ? 84 : 86;
    // Original template: brand-led, geometrical, no photography or copied reference art.
    const gradient = ctx.createLinearGradient(0, 0, width, height);
    gradient.addColorStop(0, "#120E29");
    gradient.addColorStop(.55, brandColor);
    gradient.addColorStop(1, "#0D1025");
    ctx.fillStyle = gradient; ctx.fillRect(0, 0, width, height);
    ctx.save();
    ctx.globalAlpha = 0.11;
    ctx.strokeStyle = accent;
    for (let i = 0; i < 6; i++) {
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(width * .87, height * .25, 100 + i * 74, 0, Math.PI * 2);
      ctx.stroke();
    }
    ctx.restore();
    roundRect(ctx, margin, square ? 88 : 65, 66, 7, 3, accent);
    drawWrapped(ctx, brand.toUpperCase(), margin, square ? 155 : 130, width - 2 * margin,
      { fontWeight: 750, initialSize: square ? 32 : 27, minSize: 20, maxLines: 1, color: "#FFF2D9" });
    ctx.font = "600 17px system-ui, sans-serif";
    ctx.fillStyle = "#CDC7DE";
    ctx.fillText("ORIGINAL BRAND CONCEPT", margin, square ? 225 : 192);
    const baseline = square ? 405 : 302;
    const last = drawWrapped(ctx, headline, margin, baseline, square ? width - 2 * margin : 850,
      { initialSize: square ? 86 : 76, minSize: 38, maxLines: square ? 4 : 3, color: "#FFFFFF" });
    const ctaY = Math.min(Math.max(last + 76, square ? 775 : 520), height - 174);
    const ctaWidth = Math.min(square ? 720 : 690, width - margin * 2);
    roundRect(ctx, margin, ctaY, ctaWidth, square ? 92 : 82, 18, accent);
    drawWrapped(ctx, callToAction, margin + 25, ctaY + (square ? 61 : 55), ctaWidth - 50,
      { fontWeight: 700, initialSize: 29, minSize: 20, maxLines: 1, color: "#131021" });
    ctx.fillStyle = "#FFFFFF";
    ctx.globalAlpha = .82;
    ctx.fillRect(margin, height - 92, width - 2 * margin, 1);
    ctx.globalAlpha = 1;
    ctx.font = "500 15px system-ui, sans-serif";
    ctx.fillStyle = "#E4DDEB";
    ctx.fillText("DRAFT CREATIVE • HUMAN REVIEW REQUIRED • NOT A LIVE AD", margin, height - 54);
    if (market && ctx.measureText(market).width < 220) {
      ctx.textAlign = "right";
      ctx.fillText(market, width - margin, height - 54);
      ctx.textAlign = "left";
    }
    return new Promise((resolve, reject) => {
      canvas.toBlob(blob => blob && blob.size ? resolve(blob) : reject(new Error("Unable to render PNG.")), "image/png");
    });
  }
  window.AlphaTemplateComposer = Object.freeze({ render });
})();

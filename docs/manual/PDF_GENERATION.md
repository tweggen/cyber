# Generating a PDF from the Cyber Manual

## Quick Start

The Cyber platform manual is distributed as an HTML file that is **fully optimized for print-to-PDF**. This approach works on all operating systems without requiring additional software installation.

### Method 1: Browser Print-to-PDF (Recommended)

**Step 1:** Open the HTML manual in your browser
```bash
open docs/manual/dist/manual.html
# or on Linux: xdg-open docs/manual/dist/manual.html
# or on Windows: start docs/manual/dist/manual.html
```

**Step 2:** Print the page (or use Print Preview)
- **macOS:** Cmd+P
- **Windows/Linux:** Ctrl+P

**Step 3:** Select "Save as PDF" as the destination
- Click the "PDF" dropdown menu at the bottom
- Select "Save as PDF"
- Name the file: `cyber-manual.pdf`
- Click "Save"

**Step 4:** Optional - adjust print settings for best results
- **Margins:** Minimum (0.5")
- **Scale:** 100%
- **Headers/Footers:** Uncheck to remove
- **Background graphics:** Check to include diagrams

### Method 2: Using macOS Preview

If you already have the HTML file open in macOS:
1. Press **Cmd+P** to open Print dialog
2. Click **PDF** dropdown in bottom-left
3. Select **Save as PDF**
4. Enter filename and location
5. Click **Save**

### Method 3: Command-line Options

If you have `wkhtmltopdf` installed:
```bash
wkhtmltopdf docs/manual/dist/manual.html docs/manual/dist/cyber-manual.pdf
```

If you have Chromium or Chrome installed:
```bash
chromium --headless --disable-gpu --print-to-pdf=cyber-manual.pdf docs/manual/dist/manual.html
```

## File Information

- **Source File:** `docs/manual/dist/manual.html` (440 KB)
- **Expected PDF Size:** ~2-3 MB (when generated with print-to-PDF)
- **Pages:** ~250+ pages (depending on margins and zoom)
- **Format:** A4/Letter, professional layout with TOC
- **Content:** All 16 chapters, 28 workflows, visual diagrams

## Print Quality Notes

The HTML file includes professional styling with:
- ✓ Full table of contents
- ✓ All diagrams rendered as ASCII art (crisp in PDF)
- ✓ Code blocks with syntax highlighting
- ✓ Responsive tables
- ✓ Professional typography
- ✓ Print-optimized colors (converts to B&W cleanly)

## Troubleshooting

**PDF is blank or incomplete:**
- Try printing to PDF without background graphics first
- If using Chrome, disable "Headers and footers"
- Try increasing timeout in CLI tools

**Text is too small:**
- Adjust browser zoom (Cmd+/Ctrl+) before printing
- Check "Shrink to fit" in print dialog

**Images/diagrams are missing:**
- Make sure "Background graphics" is enabled in print dialog
- Try opening HTML in different browser
- Verify all static assets loaded (check browser console for errors)

## Recommended Setup

For best results:
1. Open `manual.html` in your primary browser (Chrome, Safari, Firefox)
2. Use margin setting of 0.75 inches
3. Set scaling to 100%
4. Remove headers/footers before saving
5. Verify content displays correctly before printing

---

**Last updated:** February 21, 2026

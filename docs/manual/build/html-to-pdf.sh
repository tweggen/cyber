#!/bin/bash
# HTML to PDF Conversion Script for Cyber Manual
#
# This script converts the generated HTML manual to PDF using available tools.
# Supported tools (in order of preference):
#   1. Chromium/Chrome headless mode
#   2. wkhtmltopdf (if installed)
#   3. macOS open command with lp
#
# Usage: ./html-to-pdf.sh [input.html] [output.pdf]

set -e

# Default paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../" && pwd)"
HTML_FILE="${1:-$REPO_ROOT/docs/manual/dist/manual.html}"
PDF_FILE="${2:-$REPO_ROOT/docs/manual/dist/cyber-manual.pdf}"

echo "🔄 Converting HTML to PDF..."
echo "Input:  $HTML_FILE"
echo "Output: $PDF_FILE"

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Try Chromium/Chrome (most reliable)
if command_exists chromium; then
    echo "✓ Using Chromium..."
    chromium --headless --disable-gpu --print-to-pdf="$PDF_FILE" "$HTML_FILE" 2>/dev/null
    echo "✓ PDF generated successfully: $PDF_FILE"
    exit 0
elif command_exists google-chrome; then
    echo "✓ Using Google Chrome..."
    google-chrome --headless --disable-gpu --print-to-pdf="$PDF_FILE" "$HTML_FILE" 2>/dev/null
    echo "✓ PDF generated successfully: $PDF_FILE"
    exit 0
elif command_exists "Google Chrome"; then
    echo "✓ Using Google Chrome (macOS)..."
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless --disable-gpu --print-to-pdf="$PDF_FILE" "$HTML_FILE" 2>/dev/null
    echo "✓ PDF generated successfully: $PDF_FILE"
    exit 0
fi

# Try wkhtmltopdf
if command_exists wkhtmltopdf; then
    echo "✓ Using wkhtmltopdf..."
    wkhtmltopdf "$HTML_FILE" "$PDF_FILE"
    echo "✓ PDF generated successfully: $PDF_FILE"
    exit 0
fi

# Fallback: Print instructions for manual generation
echo ""
echo "❌ No automated PDF generation tools found."
echo ""
echo "📋 To generate PDF manually:"
echo ""
echo "1. Open the HTML file in your browser:"
echo "   open '$HTML_FILE'"
echo ""
echo "2. Press Cmd+P (macOS) or Ctrl+P (Windows/Linux)"
echo ""
echo "3. Select 'Save as PDF' as the destination"
echo ""
echo "4. Configure print settings:"
echo "   • Margins: 0.75 inches"
echo "   • Scale: 100%"
echo "   • Uncheck headers/footers"
echo "   • Check background graphics"
echo ""
echo "5. Save to: $PDF_FILE"
echo ""
echo "📖 For detailed instructions, see: $REPO_ROOT/docs/manual/PDF_GENERATION.md"
exit 1

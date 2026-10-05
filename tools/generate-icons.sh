#!/usr/bin/env bash
# Generates all icons from src/CloudflareTray/Assets/portal.svg (24×24, strokes in currentColor).
# Requires ImageMagick with librsvg (Fedora: dnf install ImageMagick librsvg2).
#
#   app.ico / app.png / app.svg  App icon: white glyph on a blue tile (window, taskbar, Explorer)
#   tray/tray-<color>-<status>   Tray glyph; black for light panels, white for dark ones.
#                                The dot in the tunnel shows the status: idle (no dot), running, reconnecting.
#                                .ico for Windows, .png for Linux/macOS
set -euo pipefail

ASSETS="$(cd "$(dirname "$0")/../src/CloudflareTray/Assets" && pwd)"
SVG="$ASSETS/portal.svg"

APP_SIZES=(16 20 24 32 40 48 64 256)
TRAY_SIZES=(16 20 24 32 40 48)
TRAY_PNG_SIZE=48

TILE_COLOR="#2563EB"
RUNNING_COLOR="#2EA043"       # same as the status dots in the main window
RECONNECTING_COLOR="#D29922"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# SVG content without the <svg> wrapper and without the small dot (replaced depending on the status)
GLYPH="$(sed -e '/<svg/d' -e '/<\/svg>/d' "$SVG")"
GLYPH_NO_DOT="$(printf '%s\n' "$GLYPH" | sed -E '/<circle/d')"

svg_open='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">'

# render <svg-file> <size> <output> – rendered directly at the target size instead of downscaled
render() {
    magick -background none -density $(( 96 * $2 / 24 )) "$1" -resize "$2x$2" "png32:$3"
}

# ico <svg-file> <output> <sizes…>
ico() {
    local svg="$1" out="$2"; shift 2
    local pngs=()
    for size in "$@"; do
        render "$svg" "$size" "$TMP/ico-$size.png"
        pngs+=("$TMP/ico-$size.png")
    done
    magick "${pngs[@]}" "$out"
}

echo "Generating icons in $ASSETS:"

# App icon: glyph scaled down on the tile, with slightly bolder strokes and a larger dot
cat > "$TMP/app.svg" <<EOF
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
  <rect width="24" height="24" rx="5.5" fill="$TILE_COLOR"/>
  <g transform="translate(4.2 3.6) scale(0.65)" fill="none" stroke="#ffffff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">
    $(printf '%s\n' "$GLYPH_NO_DOT" | sed 's/currentColor/#ffffff/g')
    <circle cx="12" cy="13" r="1.6" fill="#ffffff" stroke="none"/>
  </g>
</svg>
EOF
ico "$TMP/app.svg" "$ASSETS/app.ico" "${APP_SIZES[@]}"
render "$TMP/app.svg" 256 "$ASSETS/app.png"
cp "$TMP/app.svg" "$ASSETS/app.svg"
echo "  app.ico (${APP_SIZES[*]} px), app.png (256 px), app.svg"

# Tray: color × status
mkdir -p "$ASSETS/tray"
for color in black white; do
    [[ $color == black ]] && hex="#000000" || hex="#ffffff"
    for status in idle running reconnecting; do
        case $status in
            idle)         dot="" ;;
            running)      dot="<circle cx=\"12\" cy=\"13\" r=\"3\" fill=\"$RUNNING_COLOR\" stroke=\"none\"/>" ;;
            reconnecting) dot="<circle cx=\"12\" cy=\"13\" r=\"3\" fill=\"$RECONNECTING_COLOR\" stroke=\"none\"/>" ;;
        esac
        svg="$TMP/tray-$color-$status.svg"
        { echo "$svg_open"; printf '%s\n' "$GLYPH_NO_DOT" | sed "s/currentColor/$hex/g"; echo "$dot"; echo '</svg>'; } > "$svg"
        # Without an explicit stroke, the lines inherit it from the <svg> element
        sed -i "s|<svg |<svg stroke=\"$hex\" |" "$svg"

        ico "$svg" "$ASSETS/tray/tray-$color-$status.ico" "${TRAY_SIZES[@]}"
        render "$svg" "$TRAY_PNG_SIZE" "$ASSETS/tray/tray-$color-$status.png"
    done
done
echo "  tray/tray-{black,white}-{idle,running,reconnecting}.{ico,png}"

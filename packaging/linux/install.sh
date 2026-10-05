#!/usr/bin/env bash
# Installs Cloudflare Tunnel Manager for the current user (no root, no .NET required).
# Ships in the archive next to the app/ folder.
#
#   ./install.sh              install or update
#   ./install.sh --autostart  also start on login
#   ./install.sh --uninstall  remove again (profiles and logs in ~/.config/CloudflareTray are kept)
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
DATA="${XDG_DATA_HOME:-$HOME/.local/share}"
CONFIG="${XDG_CONFIG_HOME:-$HOME/.config}"
APP_DIR="$DATA/cloudflaretray"
BIN="${XDG_BIN_HOME:-$HOME/.local/bin}/cloudflaretray"
DESKTOP="$DATA/applications/cloudflaretray.desktop"
AUTOSTART="$CONFIG/autostart/cloudflaretray.desktop"
ICON_PNG="$DATA/icons/hicolor/256x256/apps/cloudflaretray.png"
ICON_SVG="$DATA/icons/hicolor/scalable/apps/cloudflaretray.svg"

refresh() {
    command -v update-desktop-database >/dev/null && update-desktop-database -q "$DATA/applications" || true
    command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q -t "$DATA/icons/hicolor" || true
}

stop_running() {
    # Stop a running instance, otherwise files in use would be replaced.
    # Detected via the executable: the process name depends on whether it was started via the symlink.
    local pids=() proc
    for proc in /proc/[0-9]*; do
        [[ "$(readlink "$proc/exe" 2>/dev/null)" == "$APP_DIR/CloudflareTray" ]] && pids+=("${proc#/proc/}")
    done
    [[ ${#pids[@]} -eq 0 ]] && return

    # SIGTERM: the app stops its cloudflared processes on the way out
    kill -TERM "${pids[@]}" 2>/dev/null || true
    for _ in {1..20}; do
        kill -0 "${pids[@]}" 2>/dev/null || break
        sleep 0.5
    done
    echo "Stopped the running Cloudflare Tunnel Manager."
}

if [[ "${1:-}" == "--uninstall" ]]; then
    stop_running
    rm -rf "$APP_DIR"
    rm -f "$BIN" "$DESKTOP" "$AUTOSTART" "$ICON_PNG" "$ICON_SVG"
    refresh
    echo "Cloudflare Tunnel Manager removed. Profiles and logs are still in $CONFIG/CloudflareTray."
    exit 0
fi

if [[ ! -x "$HERE/app/CloudflareTray" ]]; then
    echo "app/CloudflareTray is missing – please run install.sh from the unpacked archive." >&2
    exit 1
fi

stop_running
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR" "$(dirname "$BIN")" "$(dirname "$DESKTOP")" "$(dirname "$ICON_PNG")" "$(dirname "$ICON_SVG")"
cp -a "$HERE/app/." "$APP_DIR/"
ln -sf "$APP_DIR/CloudflareTray" "$BIN"
cp "$HERE/cloudflaretray.png" "$ICON_PNG"
cp "$HERE/cloudflaretray.svg" "$ICON_SVG"

# Absolute path, because ~/.local/bin is not in the desktop environment's PATH in every session
sed "s|^Exec=.*|Exec=$APP_DIR/CloudflareTray|" "$HERE/cloudflaretray.desktop" > "$DESKTOP"

if [[ "${1:-}" == "--autostart" ]]; then
    mkdir -p "$(dirname "$AUTOSTART")"
    cp "$DESKTOP" "$AUTOSTART"
    echo "Autostart set up."
fi

refresh
echo "Installed – find it in the application menu as \"Cloudflare Tunnel Manager\"."

# Hints about missing requirements; otherwise the app only reports them on the first tunnel
command -v cloudflared >/dev/null || echo "Note: cloudflared is not installed (see README.txt)."
command -v secret-tool >/dev/null || echo "Note: secret-tool is missing (package libsecret or libsecret-tools)."

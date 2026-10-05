Cloudflare Tunnel Manager @VERSION@ for Linux
=============================================

Installation (no root required):

    ./install.sh              install or update
    ./install.sh --autostart  also start on login
    ./install.sh --uninstall  remove again

Then start "Cloudflare Tunnel Manager" from the application menu.

Requirements
------------
- cloudflared @CLOUDFLARED_VERSION@ or newer, in the PATH.
  Fedora/RHEL:   https://pkg.cloudflare.com (set up the repository), then dnf install cloudflared
  Debian/Ubuntu: https://pkg.cloudflare.com, then apt install cloudflared
- secret-tool and a keyring (GNOME Keyring or KWallet) for the service token secrets.
  Fedora: dnf install libsecret     Debian/Ubuntu: apt install libsecret-tools
- GNOME: the "AppIndicator and KStatusNotifierItem Support" extension for the tray icon.
  Fedora: dnf install gnome-shell-extension-appindicator, then log out and back in.
  The app also works without the extension; closing the window then only minimizes it.

Profiles
--------
Profiles live in ~/.config/CloudflareTray/profiles. To use a profile someone
shared with you, just drop name.config.json (and optionally name.credentials.json)
there – it shows up immediately. Plain-text secrets are encrypted on first load.

Logs: ~/.config/CloudflareTray/logs

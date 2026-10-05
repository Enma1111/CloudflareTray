Cloudflare Tunnel Manager @VERSION@ for Windows
===============================================

Installation (no administrator rights required):

    Double-click install.cmd

    Or from a command prompt:
    install.cmd -Autostart   also start on login
    install.cmd -Uninstall   remove again

Then start "Cloudflare Tunnel Manager" from the Start menu.
You can also uninstall it via Settings > Apps.

Note: The app is not signed. If a SmartScreen warning appears on first launch,
choose "More info" > "Run anyway".

Requirements
------------
- cloudflared @CLOUDFLARED_VERSION@ or newer:
      winget install --id Cloudflare.cloudflared
  Restart the app afterwards so it picks up the new PATH.

Profiles
--------
Profiles live in %APPDATA%\CloudflareTray\profiles. To use a profile someone
shared with you, just drop name.config.json (and optionally name.credentials.json)
there – it shows up immediately. Plain-text secrets are encrypted on first load.

Logs: %APPDATA%\CloudflareTray\logs

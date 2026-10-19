import { mkdir, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import os from 'node:os';
if (process.platform !== 'darwin') throw new Error('This helper installs a macOS login service.');
const root = fileURLToPath(new URL('../', import.meta.url));
const label = 'com.coup.local-backend';
const agents = path.join(os.homedir(), 'Library', 'LaunchAgents');
await mkdir(agents, { recursive: true });
await mkdir(path.join(root, '.data'), { recursive: true, mode: 0o700 });
const escape = s => s.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
const plist = path.join(agents, label + '.plist');
await writeFile(plist, `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>Label</key><string>${label}</string>
<key>ProgramArguments</key><array><string>${escape(process.execPath)}</string><string>${escape(path.join(root, 'scripts/local-host.mjs'))}</string></array>
<key>WorkingDirectory</key><string>${escape(root)}</string>
<key>EnvironmentVariables</key><dict><key>PATH</key><string>/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin</string></dict>
<key>RunAtLoad</key><true/>
<key>KeepAlive</key><true/>
<key>ThrottleInterval</key><integer>30</integer>
<key>ExitTimeOut</key><integer>35</integer>
<key>StandardOutPath</key><string>${escape(path.join(root, '.data/host.log'))}</string>
<key>StandardErrorPath</key><string>${escape(path.join(root, '.data/host-error.log'))}</string>
</dict></plist>`, { mode: 0o600 });
const domain = `gui/${process.getuid()}`;
try { execFileSync('/bin/launchctl', ['bootout', `${domain}/${label}`], { stdio: 'ignore' }); } catch { /* First install. */ }
// launchd can report bootout before the old job has finished being removed.
let installed = false;
for (let attempt = 0; attempt < 60; attempt++) {
  try {
    execFileSync('/bin/launchctl', ['bootstrap', domain, plist], { stdio: 'pipe' });
    installed = true; break;
  } catch (err) {
    if (attempt === 59) throw new Error('launchd could not load the service. Check the plist with plutil and inspect launchctl print.');
    await new Promise(resolve => setTimeout(resolve, 500));
  }
}
if (!installed) throw new Error('Local service installation failed.');
console.log('Installed com.coup.local-backend. Starts at login; restarts after failures.');

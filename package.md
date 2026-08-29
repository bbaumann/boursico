# Packaging a Release

How to rebuild the handover archives given to the person who hosts the
application. Written in English to match the other developer-facing documents;
the operator guide shipped inside the archives is in French.

## TL;DR

```powershell
pwsh tools/Package-Release.ps1
```

Produces in `dist/` (gitignored):

| Archive | Zipped | Unpacked |
|---|---|---|
| `bourse-aux-jouets-linux-x64.zip` | ~46 MB | ~112 MB |
| `bourse-aux-jouets-win-x64.zip` | ~48 MB | ~115 MB |

## Prerequisites

- .NET SDK matching `global.json` (8.0.403, `rollForward: latestPatch`)
- `git` on PATH — the script stamps the commit into `VERSION.txt`

## Pre-flight checklist

**The operator guide deliberately contains no configuration step.** The person
hosting the application only unzips and runs it, so `appsettings.json` must
already hold the real values *before* packaging:

- [ ] `Admin:SharedPassword` — the script prints a warning if it is still
      `tada` or `change-me`, but does not stop
- [ ] `Event:Name` — shown in the page titles
- [ ] `Printing:InvoicePeriod` — `YYMM` of the event, used in invoice numbers
- [ ] `Database:SqlitePath` — `data/bourse-aux-jouets.db`, resolved relative to
      the application directory
- [ ] Working tree committed, so the `VERSION.txt` stamp is meaningful

## What the script does

For each runtime identifier:

1. `dotnet publish -c Release -r <rid> --self-contained true`
2. Deletes `appsettings.Development.json` from the output
3. Copies `deploy/INSTALLATION.md` and the matching launcher
   (`demarrer.sh` for Linux, `demarrer.bat` for Windows)
4. Writes `VERSION.txt` (version, runtime, build date, full commit SHA)
5. Zips the staging folder into `dist/`

Staging lives in `.tmp/package/<rid>/` and is wiped on each run.

Useful switches:

```powershell
pwsh tools/Package-Release.ps1 -Runtime linux-x64      # Debian only
pwsh tools/Package-Release.ps1 -Version 2026-11-prod   # override the stamp
```

## Operator-facing files

These live in `deploy/` and are the ones to edit when the handover
instructions change:

- `deploy/INSTALLATION.md` — the French installation guide
- `deploy/demarrer.sh` — Linux launcher, LF-only (enforced by `.gitattributes`)
- `deploy/demarrer.bat` — Windows launcher, CRLF

Both launchers `cd` into their own directory before starting the executable.
This matters: `Database:SqlitePath` is relative to the application directory,
so launching the binary from elsewhere silently creates a second, empty
database.

## Verifying before handover

Extract the win-x64 archive somewhere empty and run it. A correct first start
prints ~40 lines — the EF migrations, then:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5050
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

Then check `http://localhost:5050` returns the sellers page, and that a `data/`
folder was created holding `.db`, `.db-wal` and `.db-shm`. Nothing should be
logged at `warn:` or above.

## Decisions worth remembering

**Self-contained, not framework-dependent.** A portable build is only ~16 MB
zipped, but requires `aspnetcore-runtime-8.0` on the target. That package is
easy to confuse with `dotnet-runtime-8.0` (which is *not* enough and fails
cryptically), and its availability on Debian 13 is not something we want to
depend on. Trading ~30 MB of transfer for removing an install step is worth it.

**No systemd unit.** The application is started by hand for an event and
stopped afterwards, so the launcher scripts are the right level of ceremony.
If it ever needs to run permanently, a unit file with
`WorkingDirectory=/opt/bourse` and `Restart=always` is the drop-in.

**ICU is the only Linux dependency.** .NET needs the OS ICU library even in a
self-contained build. Present on most Debian installs, absent on minimal ones —
hence step 1 of the guide. Setting `InvariantGlobalization` would remove it but
would also break French date and number formatting.

**The zip loses the Unix executable bit**, because it is built on Windows.
That is why the guide opens with `chmod +x`. Any change to how the archive is
produced must keep that instruction accurate.

**EF command logging is set to `Warning`** in `appsettings.json`. At
`Information` the first start emits ~700 lines of SQL and buries the
`Application started` line the guide tells the operator to wait for.

## Changing the port

The port is read from the `PORT` environment variable and defaults to `5050`
(`Program.cs`). To ship a different default, change it there and update
`deploy/INSTALLATION.md`, which mentions 5050 in several places.

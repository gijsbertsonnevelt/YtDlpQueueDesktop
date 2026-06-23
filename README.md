# yt-dlp Queue (Desktop / WinForms)

Native Windows-app (.NET 10, WinForms) die yt-dlp aanstuurt met een parallelle downloadwachtrij.
Geen browser nodig — alles draait in een los venster, net als je andere GS Software-tools.

Dit vervangt de eerdere Blazor-versie. De kernlogica (de wachtrij met meerdere parallelle
workers, de voortgangs-parsing van yt-dlp's output) is hetzelfde gebleven; alleen de UI-laag is
nu WinForms in plaats van een webpagina.

## Vereisten

1. **.NET 10 SDK** — al aanwezig op je dev-machine.
2. **yt-dlp.exe** — download van https://github.com/yt-dlp/yt-dlp/releases. Zet het op je PATH,
   of stel het pad in via het **Instellingen**-knopje in de app.
3. **ffmpeg** — nodig zodra yt-dlp video en audio apart moet downloaden en samenvoegen (dus ook
   voor normale video-downloads in goede kwaliteit, niet alleen voor de mp3-optie). Download van
   https://www.gyan.dev/ffmpeg/builds/ ("release-essentials"), pak uit, en zet de `bin`-map op je
   PATH. Herstart Visual Studio na het aanpassen van de PATH.

## Instellingen

Klik in de app op **Instellingen** om in te stellen:
- Pad naar `yt-dlp.exe` (laat staan als het op de PATH staat)
- Outputmap voor downloads (standaard: `%UserProfile%\Downloads\YtDlpQueue`)
- Aantal gelijktijdige downloads (wijziging vereist herstart van de app)

Instellingen worden opgeslagen in:
```
%LocalAppData%\GS Software\YtDlpQueue\config.json
```
(zelfde patroon als de config van LD2410 Lock.)

## Draaien

Open `YtDlpQueueDesktop.csproj` in Visual Studio (vereist de workload "Desktopontwikkeling met
.NET" i.p.v. de webworkload) en druk op F5 of Ctrl+F5. Er opent een los venster — geen browser.

Of via de command line:
```bash
dotnet run
```

## Hoe het werkt

- `Services/DownloadQueueService.cs` is de kern: een `Channel<DownloadItem>` als wachtrij, met
  bij start een vast aantal worker-taken (`Task.Run`) die continu items pakken en yt-dlp als
  subprocess draaien — dus écht parallel.
- Voortgang wordt geparsed uit de `[download] ...%` regels die yt-dlp met `--newline` per regel
  uitstuurt. Best-effort regex-parsing; als yt-dlp het outputformaat wijzigt, kan de regex in
  `ProgressRegex` bijgewerkt moeten worden.
- `Controls/DataGridViewProgressCell.cs` is een zelfgeschreven voortgangsbalk-cel, omdat
  WinForms' DataGridView die niet standaard heeft.
- De UI (`MainForm.cs`) abonneert zich op het `OnChange`-event van de service. Omdat dat event op
  een achtergrondthread kan vuren (de yt-dlp-procesoutput komt binnen via threadpool-callbacks),
  wordt elke update via `BeginInvoke` netjes naar de UI-thread gestuurd voordat de grid wordt
  bijgewerkt.
- De wachtrij leeft alleen in het geheugen van het proces; sluit je de app, dan is de wachtrij
  leeg bij de volgende start. Annuleren stuurt een cancellation die het yt-dlp-proces hard killt.
- De app-manifest (`app.manifest`) zet per-monitor DPI-bewustzijn aan (PerMonitorV2), zodat het
  venster scherp blijft op verschillende schermen/schaalinstellingen — hetzelfde probleem als je
  bij LD2410 Lock had opgelost.

## Mogelijke uitbreidingen

- Wachtrij persisteren naar schijf zodat een herstart niet alles kwijt is.
- Systeemvak-icoon (NotifyIcon) zodat de app naar de achtergrond kan, net als LD2410 Lock.
- Kwaliteitskeuze (bv. 1080p/720p) in plaats van automatisch beste video+audio.
- WiX-installer, net als bij LD2410 Lock, in plaats van handmatig `dotnet run`.

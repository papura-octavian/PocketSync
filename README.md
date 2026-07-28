# PocketSync

Transfer de fișiere între telefon și PC, în rețeaua locală — fără cloud, fără cablu, fără cont.

PC-ul rulează un server HTTP local (tray app). Telefonul vorbește cu el prin WiFi, din browser sau din aplicația Android. Nimic nu iese din LAN.

## Funcționalități

- 📤 **Telefon → PC**: trimiți fișiere din browser sau cu **Share → PocketSync** din orice aplicație Android
- 📥 **PC → Telefon**: vezi fișierele din folderul `Storage` și le descarci cu un tap
- 🔒 **Pairing cu QR**: scanezi codul o dată, tokenul rămâne salvat; nimeni altcineva de pe WiFi nu are acces
- 🗑️ Ștergere de pe telefon (în Recycle Bin, recuperabil)
- ⏻ **Comenzi PC**: blochează / sleep / restart / shutdown de pe telefon (cu confirmare și 15 secunde de anulare)
- 🖥️ Tray app Windows: QR la un click, notificare la fișier primit, pornire cu Windows (opțional), log-uri

## Instalare

**PC (Windows 10/11 x64):** descarcă `PocketSync-Setup-x.x.x.exe` din [Releases](../../releases), instalează (pune singur regula de firewall), pornește PocketSync din Start Menu.

**Telefon:**
- *Varianta simplă*: scanează QR-ul din fereastra PocketSync cu camera — se deschide în browser, gata.
- *Varianta cu share sheet*: instalează `PocketSync.apk` din [Releases](../../releases) (sideload), deschide aplicația și lipește link-ul de pairing (butonul „Copiază link" de pe PC).

## Structură

| Proiect | Ce e |
|---|---|
| `PocketSync/` | serverul ASP.NET Core (endpoint-uri, auth, static files) + gazdă de consolă pentru development |
| `PocketSync.TrayApp/` | aplicația Windows de tray (WPF) care găzduiește serverul |
| `PocketSync.Mobile/` | aplicația Android (.NET MAUI) cu share sheet |
| `installer/` | scriptul Inno Setup |
| `Documentation/` | planul de implementare pe faze (vault Obsidian) |

## Securitate — ce e și ce nu e

- Token aleatoriu criptografic, salvat criptat (DPAPI pe Windows, Keystore pe Android), comparat în timp constant. Fără token: 401 la tot în afară de `/api/status`.
- **Fără HTTPS** — traficul e în clar în LAN-ul tău. Compromis asumat pentru uz personal acasă (self-signed e o fundătură pe Android). Nu expune serverul pe internet; pentru acces din afara casei folosește Tailscale.

## Build din sursă

Necesită .NET 10 SDK (+ workload-ul MAUI pentru aplicația Android).

```
dotnet build PocketSync.slnx
dotnet publish PocketSync.TrayApp -c Release -r win-x64 --self-contained -o publish/win-x64
dotnet publish PocketSync.Mobile -f net10.0-android -c Release -o publish/android
```

Installer-ul: `ISCC.exe installer\PocketSync.iss` (Inno Setup 6).

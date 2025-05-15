# Jurnal de modificări (Changelog)

## Română

### [1.0.1.0] - 2025-05-03

#### Adăugat
- Efecte sonore la click-urile pe butoanele de browser
- Efecte sonore la acțiunile de ascundere/afișare a ferestrei de jurnal
- Efecte sonore la click-urile de expandare a combo box-urilor
- Jurnalizare îmbunătățită în SoundPlayer pentru diagnosticarea problemelor de redare a sunetului
- Suport pentru fișier local settings.json în plus față de locația AppData
- Posibilitatea de a șterge fișierele selectate când nu se alege niciun fișier în dialogul de selecție
- Îmbunătățiri pentru copierea în clipboard cu mecanism de reîncercare pentru a evita erorile

#### Modificat
- Mecanism îmbunătățit de redare a sunetului cu gestionare mai bună a erorilor și opțiuni de rezervă
- Capacități de jurnalizare îmbunătățite cu informații mai detaliate
- Mecanism de încărcare a configurației actualizat pentru a gestiona mai bine proprietățile lipsă
- Adăugat suport pentru framework-ul .NET 10.0
- Îmbunătățiri pentru mesajele de jurnal pentru a reduce spam-ul și a menține timestamp-uri consistente
- Optimizări pentru rotația reclamelor și gestionarea imaginilor

#### Remediat
- Remediate probleme de redare a sunetului prin adăugarea de jurnalizare detaliată și descoperire îmbunătățită a resurselor
- Remediate probleme de încărcare a assembly-urilor în SoundPlayer
- Eroarea "OpenClipboard Failed" la copierea textului în clipboard
- Probleme cu rotația imaginilor în managerul de reclame
- Probleme cu formatarea notelor de lansare în fereastra de actualizare

#### Eliminat
- Imagine debug.png neutilizată din proiectul CSVGenerator

## English

### [1.0.1.0] - 2025-05-03

#### Added
- Sound effects on browser button clicks
- Sound effects on log window hide/unhide actions
- Sound effects on combo box expansion clicks
- Enhanced logging in SoundPlayer to help diagnose sound playback issues
- Support for local settings.json file in addition to AppData location
- Ability to clear selected files when no file is chosen in the file selection dialog
- Improved clipboard copying with retry mechanism to avoid errors

#### Changed
- Improved sound playback mechanism with better error handling and fallback options
- Enhanced logging capabilities with more detailed information
- Updated configuration loading mechanism to handle missing properties more gracefully
- Added support for .NET 10.0 framework
- Improved logging messages to reduce spam and maintain consistent timestamps
- Optimizations for ad rotation and image handling

#### Fixed
- Fixed sound playback issues by adding detailed logging and improved resource discovery
- Fixed assembly loading issues in SoundPlayer
- "OpenClipboard Failed" error when copying text to clipboard
- Issues with image rotation in the ad manager
- Issues with formatting release notes in the update window

#### Removed
- Unused debug.png image from CSVGenerator project

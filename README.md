# Generator CSV (CSVGenerator)

## Română

### Prezentare generală

Generator CSV este o aplicație WPF specializată care procesează fișiere BOM (Bill of Materials) și PINS (format .asc) și generează fișiere CSV standardizate pentru utilizare în procese de producție. Aplicația suportă multipli clienți, menține un istoric al programelor și oferă conversie automată de unități între inch și centimetri.

### Versiune curentă

**1.0.1.0** - [Vezi jurnalul de modificări](CHANGELOG.md)

### Caracteristici

- Încărcare și procesare fișiere BOM și PINS (format .asc)
- Detectare automată a formatului fișierului și a structurii
- Dropdown client cu istoric și auto-completare
- Introducere program cu istoric și sugestii
- Conversie unități (inch/centimetri) cu detectare automată și posibilitate de suprascriere manuală
- Validare și normalizare a datelor de intrare
- Suport multi-limbaj (Română/Engleză) cu comutare rapidă
- Jurnalizare detaliată cu coduri de eroare și sugestii de rezolvare
- Sistem de reclame cu suport pentru text și imagini
- Efecte sonore la interacțiunile utilizatorului pentru feedback îmbunătățit
- Posibilitatea de a șterge fișierele selectate când nu se alege niciun fișier

### Arhitectură

Aplicația este structurată pe mai multe componente:

- **UI Layer**: Interfața utilizator WPF cu MVVM
- **Business Logic Layer**: Procesarea și transformarea datelor
- **Data Access Layer**: Citirea și scrierea fișierelor
- **Common Library**: Componente partajate cu alte aplicații

### Primii pași

#### Cerințe sistem

- **Sistem de operare**: Windows 7 SP1 sau mai nou
- **Framework**:
  - .NET Framework 4.8 sau mai nou
  - .NET 5.0 până la .NET 10.0 (pentru versiunile moderne)
- **Spațiu pe disc**: Minim 50MB
- **Memorie**: Minim 2GB RAM (recomandat 4GB pentru fișiere mari)
- **Rezoluție ecran**: Minim 1280x720

#### Instalare

1. Descărcați cea mai recentă versiune din pagina [Releases](https://github.com/DarkPhilosophy/CSVGenerator/releases)
2. Extrageți fișierul ZIP în locația preferată
3. Rulați `CSVGenerator.exe`
4. La prima rulare, aplicația va încerca să încarce fișierul settings.json din directorul aplicației, iar dacă nu există, va crea automat un fișier settings.json în directorul AppData al utilizatorului

#### Construirea din sursă

##### Construirea cu Visual Studio

1. Deschideți soluția `CSVGenerator.sln` în Visual Studio 2019 sau mai nou
2. Selectați configurația (Debug/Release) și framework-ul țintă
3. Construiți soluția folosind meniul Build > Build Solution (F6)
4. Fișierele rezultate se vor găsi în directorul `bin\[Configuration]\[Framework]`

##### Construirea cu .NET CLI

Aplicația suportă multiple framework-uri țintă. Utilizați una din următoarele comenzi:

```powershell
# Pentru .NET Framework 4.8
dotnet publish CSVGenerator\CSVGenerator.csproj -r win-x64 -f net48 -c Release -o Release\CSVGenerator-net48

# Pentru .NET 6.0
dotnet publish CSVGenerator\CSVGenerator.csproj -r win-x64 -f net6.0-windows -c Release -o Release\CSVGenerator-net6.0

# Pentru .NET 10.0
dotnet publish CSVGenerator\CSVGenerator.csproj -r win-x64 -f net10.0-windows -c Release -o Release\CSVGenerator-net10.0
```

### Utilizare

#### Procesarea fișierelor BOM și PINS

1. Lansați aplicația
2. Selectați un client din dropdown sau introduceți unul nou
3. Introduceți un nume de program sau selectați din istoric
4. Faceți clic pe "Încarcă" pentru a selecta un fișier BOM sau PINS
5. Aplicația va detecta automat unitatea de măsură (inch/centimetri), dar puteți suprascrie manual
6. Faceți clic pe "Generează CSV" pentru a procesa fișierul și a crea output-ul CSV
7. Fișierul CSV generat va fi salvat în directorul specificat în setări

#### Formate de fișiere suportate

- **Fișiere BOM (Bill of Materials)**:

  - Format: Text ASCII (.asc)
  - Structură: Cu sau fără anteturi
  - Separatori: Virgulă, tab sau spațiu
  - Câmpuri obligatorii: Referință, Cantitate, Valoare, Pachet
- **Fișiere PINS**:

  - Format: Text ASCII (.asc)
  - Structură: Cu anteturi
  - Separatori: Virgulă sau tab
  - Câmpuri obligatorii: Referință, X, Y, Rotație

#### Configurare

Aplicația utilizează un fișier settings.json pentru configurare:

```json
{
  "ClientList": [
    "GEC",
    "PBEH",
    "AGI",
    "NER",
    "SEA4",
    "SEAH",
    "ADVA",
    "NOK"
  ],
  "ProgramHistory": [],
  "LastBomSplitPath": "",
  "LastCadPinsPath": "",
  "Language": "Romanian"
}
```

### Structura proiectului

```
CSVGenerator/
├── App/                     # Aplicația principală
│   ├── Program.cs           # Punct de intrare
│   └── App.xaml             # Configurare aplicație
├── Core/                    # Logica de business
│   ├── Models/              # Modele de date
│   │   ├── BomModel.cs      # Model pentru fișiere BOM
│   │   └── PinsModel.cs     # Model pentru fișiere PINS
│   ├── Services/            # Servicii pentru logica de business
│   │   ├── FileParser.cs    # Parsare fișiere
│   │   ├── CsvGenerator.cs  # Generare CSV
│   │   └── UnitConverter.cs # Conversie unități
│   └── Utilities/           # Clase utilitare
├── UI/                      # Interfața utilizator
│   ├── ViewModels/          # View models (MVVM)
│   ├── Views/               # Interfețe utilizator
│   │   ├── MainWindow.xaml  # Fereastra principală
│   │   └── LogWindow.xaml   # Fereastra de log
│   └── Controls/            # Controale personalizate
├── Properties/              # Proprietăți aplicație
├── Resources/               # Resurse încorporate
│   ├── Icons/               # Iconițe și imagini
│   ├── Langs/               # Fișiere de localizare
│   └── Sounds/              # Efecte sonore
├── CSVGenerator.csproj      # Fișier proiect
├── FodyWeavers.xml          # Configurare pentru Fody
└── settings.json            # Configurare implicită
```

### Dependențe

- [Biblioteca Common](https://github.com/DarkPhilosophy/Common) - Componente partajate pentru aplicații WPF
- [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/) - Framework de configurare modern pentru .NET
- [Microsoft.Extensions.FileSystemGlobbing](https://www.nuget.org/packages/Microsoft.Extensions.FileSystemGlobbing/) - Suport pentru pattern matching în sistemul de fișiere
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - Framework JSON pentru .NET (doar pentru .NET Framework 4.8)

### Licență

Acest proiect este licențiat sub Licența MIT - consultați fișierul [LICENSE](../LICENSE) pentru detalii.

## English

### Overview

CSVGenerator is a specialized WPF application that processes BOM (Bill of Materials) and PINS files (.asc format) and generates standardized CSV output files for use in manufacturing processes. The application supports multiple clients, maintains a history of programs, and provides automatic unit conversion between inches and centimeters.

### Current Version

**1.0.1.0** - [See changelog](CHANGELOG.md)

### Features

- Upload and process BOM and PINS files (.asc format)
- Automatic detection of file format and structure
- Client dropdown with history and auto-completion
- Program input with history and suggestions
- Unit conversion (inches/centimeters) with automatic detection and manual override capability
- Validation and normalization of input data
- Multi-language support (Romanian/English) with quick switching
- Detailed logging with error codes and resolution suggestions
- Advertisement system with support for text and images
- Sound effects on user interactions for improved feedback
- Ability to clear selected files when no file is chosen

### Architecture

The application is structured into several components:

- **UI Layer**: WPF user interface with MVVM
- **Business Logic Layer**: Data processing and transformation
- **Data Access Layer**: File reading and writing
- **Common Library**: Components shared with other applications

### Getting Started

#### System Requirements

- **Operating System**: Windows 7 SP1 or later
- **Framework**:
  - .NET Framework 4.8 or later
  - .NET 5.0 to .NET 10.0 (for modern versions)
- **Disk Space**: Minimum 50MB
- **Memory**: Minimum 2GB RAM (4GB recommended for large files)
- **Screen Resolution**: Minimum 1280x720

#### Installation

1. Download the latest release from the [Releases](https://github.com/DarkPhilosophy/CSVGenerator/releases) page
2. Extract the ZIP file to your preferred location
3. Run `CSVGenerator.exe`
4. On first run, the application will attempt to load the settings.json file from the application directory, and if it doesn't exist, it will automatically create a settings.json file in the user's AppData directory

#### Building from Source

##### Building with Visual Studio

1. Open the `CSVGenerator.sln` solution in Visual Studio 2019 or later
2. Select the configuration (Debug/Release) and target framework
3. Build the solution using the Build > Build Solution menu (F6)
4. The output files will be located in the `bin\[Configuration]\[Framework]` directory

##### Building with .NET CLI

The application supports multiple target frameworks. Use one of the following commands:

```powershell
# For .NET Framework 4.8
dotnet publish CSVGenerator\CSVGenerator.csproj -r win-x64 -f net48 -c Release -o Release\CSVGenerator-net48

# For .NET 6.0
dotnet publish CSVGenerator\CSVGenerator.csproj -r win-x64 -f net6.0-windows -c Release -o Release\CSVGenerator-net6.0

# For .NET 10.0
dotnet publish CSVGenerator\CSVGenerator.csproj -r win-x64 -f net10.0-windows -c Release -o Release\CSVGenerator-net10.0
```

### Usage

#### Processing BOM and PINS Files

1. Launch the application
2. Select a client from the dropdown or enter a new one
3. Enter a program name or select from history
4. Click "Upload" to select a BOM or PINS file
5. The application will automatically detect the unit of measurement (inches/centimeters), but you can manually override
6. Click "Generate CSV" to process the file and create CSV output
7. The generated CSV file will be saved in the directory specified in settings

#### Supported File Formats

- **BOM (Bill of Materials) Files**:

  - Format: ASCII Text
  - Structure: With or without headers
  - Separators: Comma, tab, or space
  - Required fields: Reference, Quantity, Value, Package
- **PINS Files**:

  - Format: ASCII Text (.asc)
  - Structure: With headers
  - Separators: Comma or tab
  - Required fields: Reference, X, Y, Rotation

#### Configuration

The application uses a settings.json file for configuration:

```json
{
  "ClientList": [
    "GEC",
    "PBEH",
    "AGI",
    "NER",
    "SEA4",
    "SEAH",
    "ADVA",
    "NOK"
  ],
  "ProgramHistory": [],
  "LastBomSplitPath": "",
  "LastCadPinsPath": "",
  "Language": "Romanian"
}
```

### Project Structure

```
CSVGenerator/
├── App/                     # Main application
│   ├── Program.cs           # Entry point
│   └── App.xaml             # Application configuration
├── Core/                    # Business logic
│   ├── Models/              # Data models
│   │   ├── BomModel.cs      # Model for BOM files
│   │   └── PinsModel.cs     # Model for PINS files
│   ├── Services/            # Business logic services
│   │   ├── FileParser.cs    # File parsing
│   │   ├── CsvGenerator.cs  # CSV generation
│   │   └── UnitConverter.cs # Unit conversion
│   └── Utilities/           # Utility classes
├── UI/                      # User interface
│   ├── ViewModels/          # View models (MVVM)
│   ├── Views/               # User interfaces
│   │   ├── MainWindow.xaml  # Main window
│   │   └── LogWindow.xaml   # Log window
│   └── Controls/            # Custom controls
├── Properties/              # Application properties
├── Resources/               # Embedded resources
│   ├── Icons/               # Icons and images
│   ├── Langs/               # Localization files
│   └── Sounds/              # Sound effects
├── CSVGenerator.csproj      # Project file
├── FodyWeavers.xml          # Fody configuration
└── settings.json            # Default configuration
```

### Dependencies

- [Common Library](https://github.com/DarkPhilosophy/Common) - Shared components for WPF applications
- [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/) - Modern configuration framework for .NET
- [Microsoft.Extensions.FileSystemGlobbing](https://www.nuget.org/packages/Microsoft.Extensions.FileSystemGlobbing/) - File system pattern matching support
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON framework for .NET (only for .NET Framework 4.8)

### License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.

## Author

Adalbert Alexandru Ungureanu - [adalbertalexandru.ungureanu@flex.com](mailto:adalbertalexandru.ungureanu@flex.com)

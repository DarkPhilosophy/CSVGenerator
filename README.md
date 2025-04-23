# CSVGenerator

A utility application for generating CSV files from BOM (Bill of Materials) and PINS files.

## Overview

CSVGenerator is a WPF application that processes BOM and PINS files (.asc format) and generates standardized CSV output files. It supports multiple clients, maintains a history of programs, and provides unit conversion between inches and centimeters.

## Features

- Upload and process BOM and PINS files (.asc format)
- Client dropdown with history
- Program input with history
- Unit conversion (inches/centimeters)
- Multi-language support (English/Romanian)
- Detailed logging
- Advertisement system

## Getting Started

### Prerequisites

- Windows 7 or later
- .NET Framework 4.8 or later
- .NET 6.0 or later (for newer framework targets)
- Visual Studio 2019 or later

### Installation

1. Download the latest release from the [Releases](https://github.com/DarkPhilosophy/CSVGenerator/releases) page
2. Extract the ZIP file to your preferred location
3. Run `CSVGenerator.exe`

### Building from Source

The application can be built for multiple target frameworks:

```powershell
# For .NET Framework 4.8 (framework-dependent)
dotnet publish CSVGenerator\CSVGenerator.csproj -r win-x64 -f net48 -c Release -o Release\CSVGenerator-net48-fd

# For .NET 6.0 (framework-dependent)
dotnet publish CSVGenerator\CSVGenerator.csproj -r win-x64 -f net6.0-windows -c Release -o Release\CSVGenerator-net6-fd

# For .NET 9.0 (framework-dependent)
dotnet publish CSVGenerator\CSVGenerator.csproj -r win-x64 -f net9.0-windows -c Release -o Release\CSVGenerator-net9-fd
```

## Usage

1. Launch the application
2. Select a client from the dropdown or enter a new one
3. Enter a program name or select from history
4. Click "Upload" to select a BOM or PINS file
5. Choose the unit (inches/centimeters)
6. Click "Generate CSV" to process the file and create CSV output

### File Format Support

- **BOM Files**: ASCII text files with or without headers
- **PINS Files**: ASCII text files with headers

## Project Structure

- `/src`: Source code files
- `/assets`: Resources (icons, sounds, language files)
- `/docs`: Documentation

## Dependencies

- [Common Library](https://github.com/DarkPhilosophy/Common) - Shared components for WPF applications
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON framework for .NET

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.

## Author

Adalbert Alexandru Ungureanu - [adalbertalexandru.ungureanu@flex.com](mailto:adalbertalexandru.ungureanu@flex.com)

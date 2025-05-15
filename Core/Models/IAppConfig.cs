using System.Collections.Generic;
using System.IO;

namespace CSVGenerator.Core.Models
{
    public interface IAppConfig
    {
        string Language { get; set; }
        string LastBomSplitPath { get; set; }
        string LastCadPinsPath { get; set; }
        List<string> ClientList { get; set; }
        List<string> ProgramHistory { get; set; }
        string SettingsPath { get; }
        void Save();
    }
}
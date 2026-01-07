using System;
using System.Threading.Tasks;
using XiDeAI_Pro.Services;

namespace XiDeAI_Pro.Services.Core
{
    public interface IModule
    {
        // Unique identifier for the module (e.g. "KING_TRADER", "NEWS_TRACKER")
        string ModuleName { get; }

        // Is the module currently active/running?
        bool IsActive { get; set; }

        // Start usage or initialization
        Task InitializeAsync();

        // Process a signal or data packet
        Task ProcessSignalAsync(SignalData signal);
        
        // Return current status/stats
        string GetStatus();
    }
}



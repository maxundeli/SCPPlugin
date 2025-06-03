using Exiled.API.Interfaces;
using System.ComponentModel;

namespace MaxunPlugin
{
    public class Config : IConfig
    {
        [Description("Whether the plugin is enabled.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Enable debug messages.")]
        public bool Debug { get; set; } = false;

        [Description("Connection string for the MySQL database.")]
        public string ConnectionString { get; set; } = "Server=localhost;Database=scp;User ID=maxundeli;Password=Maxx1826583ru;Pooling=true;";
    }
}

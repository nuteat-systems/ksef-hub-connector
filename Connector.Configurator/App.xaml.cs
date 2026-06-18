using System.Windows;

namespace Connector.Configurator;

public partial class App : Application
{
    public App()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }
}

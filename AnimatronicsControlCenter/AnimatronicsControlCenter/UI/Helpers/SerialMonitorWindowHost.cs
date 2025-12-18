using System;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.Views;

namespace AnimatronicsControlCenter.UI.Helpers
{
    public sealed class SerialMonitorWindowHost
    {
        private readonly IServiceProvider _services;
        private SerialMonitorWindow? _window;

        public SerialMonitorWindowHost(IServiceProvider services)
        {
            _services = services;
        }

        public void Show()
        {
            if (_window == null)
            {
                _window = _services.GetRequiredService<SerialMonitorWindow>();
                _window.Closed += (_, _) => _window = null;
                _window.Activate();
                return;
            }

            _window.Activate();
        }
    }
}



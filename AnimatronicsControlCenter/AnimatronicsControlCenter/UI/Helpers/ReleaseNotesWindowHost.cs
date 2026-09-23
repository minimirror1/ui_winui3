using System;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.Views;

namespace AnimatronicsControlCenter.UI.Helpers
{
    /// 릴리즈 노트 창을 하나만 띄운다. 이미 떠 있으면 그 창을 앞으로 가져온다.
    public sealed class ReleaseNotesWindowHost
    {
        private readonly IServiceProvider _services;
        private ReleaseNotesWindow? _window;

        public ReleaseNotesWindowHost(IServiceProvider services)
        {
            _services = services;
        }

        public void Show()
        {
            if (_window == null)
            {
                _window = _services.GetRequiredService<ReleaseNotesWindow>();
                _window.Closed += (_, _) => _window = null;
            }

            _window.Activate();
        }

        public void ApplyTheme()
        {
            _window?.ApplyTheme();
        }
    }
}

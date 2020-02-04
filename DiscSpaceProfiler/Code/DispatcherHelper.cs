using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;

namespace DiscSpaceProfiler.ViewModels
{
    public static class DispatcherHelper
    {
        [ExcludeFromCodeCoverage]
        public static void Invoke(Action action)
        {
            Invoke(App.Current?.Dispatcher, action);
        }
        [ExcludeFromCodeCoverage]
        public static void Invoke(Dispatcher dispatcher, Action action)
        {
            if (dispatcher == null)
                action();
            else
                dispatcher.Invoke(action);
        }
    }
}

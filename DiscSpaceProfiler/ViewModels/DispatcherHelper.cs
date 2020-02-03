using System;
using System.Diagnostics.CodeAnalysis;

namespace DiscSpaceProfiler.ViewModels
{
    public static class DispatcherHelper
    {
        [ExcludeFromCodeCoverage]
        public static void Invoke(Action action)
        {
            if (App.Current == null || App.Current.Dispatcher == null)
                action();
            else
                App.Current.Dispatcher.Invoke(action);
        }
    }
}

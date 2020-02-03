using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DiscSpaceProfiler.UI.Interop
{
    public static class WpfExtensions
    {
        public static System.Windows.Forms.IWin32Window GetIWin32Window(this Visual visual)
        {
            var source = PresentationSource.FromVisual(visual) as HwndSource;
            System.Windows.Forms.IWin32Window win = new WindowWrapper(source.Handle);
            return win;
        }

        private class WindowWrapper : System.Windows.Forms.IWin32Window
        {
            private readonly System.IntPtr _handle;

            public WindowWrapper(System.IntPtr handle)
            {
                _handle = handle;
            }

            System.IntPtr System.Windows.Forms.IWin32Window.Handle
            {
                get { return _handle; }
            }
        }
    }
}

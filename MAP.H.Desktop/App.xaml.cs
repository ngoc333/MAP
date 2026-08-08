using System.Windows;
using MAP.C.Wpf;

namespace MAP.H.Desktop;

public partial class App : Application
{
    public App() => WpfHost.Run(this);
}

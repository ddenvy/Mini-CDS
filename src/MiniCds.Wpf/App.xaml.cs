using System.Configuration;
using System.Data;
using System.Windows;

namespace MiniCds.Wpf;

/// <summary>
/// Interaction logic for App.xaml.
/// Base class is fully qualified: inside namespace MiniCds.Wpf the simple name
/// Application resolves to the MiniCds.Application namespace before any using alias applies.
/// </summary>
public partial class App : System.Windows.Application
{
}


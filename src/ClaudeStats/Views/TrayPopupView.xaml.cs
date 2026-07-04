using System.Windows.Controls;

namespace ClaudeStats.Views;

/// <summary>
/// Compact popup panel shown from the tray icon, listing each limit window with its usage and reset.
/// </summary>
public partial class TrayPopupView : UserControl
{
    /// <summary>Initializes the popup view.</summary>
    public TrayPopupView()
    {
        InitializeComponent();
    }
}

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyWPFApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Debug logging - writes to console (visible in terminal)
        System.Diagnostics.Debug.WriteLine("MainWindow initialized - v7 HOST RELOAD");
        Console.WriteLine("[MyWPFApp] MainWindow loaded - v7 RELOADED!");
    }
}
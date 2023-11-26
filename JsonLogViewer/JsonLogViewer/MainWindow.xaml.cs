using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace JsonLogViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
        : Window
    {
        private readonly MainVm _vm = new();
        private readonly DispatcherTimer _timer = new();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _vm;

            //_timer.Interval = TimeSpan.FromSeconds(10.0);
            //_timer.Start();

            //_timer.Tick += _timer_Tick;

            Loaded += MainWindow_Loaded;

            _dataGrid.SelectionChanged += _dataGrid_SelectionChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Loaded");

            var workdir = Environment.CurrentDirectory;
            Debug.WriteLine($"workdir = {workdir}");
            {
                var d = workdir.Replace("\\", "/");
                var i = d.LastIndexOf("JsonLogViewer/JsonLogViewer");
                Debug.Assert(i >= 0);
                d = d[..i];
                _vm.LogFile = $"{d}/default.log";
                Debug.WriteLine($"log = {_vm.LogFile}");
            }

            _vm.Reload();
        }

        private void _timer_Tick(object? sender, EventArgs e)
        {
            Debug.WriteLine("Tick");
            _vm.Reload();
        }

        private void _menuOpen_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Open");
        }

        private void _dataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var index = _dataGrid.SelectedIndex;
            var count = _vm.Items.Count;
            Debug.WriteLine($"Selected [{index}/{count}]");

            if ((uint)index >= (uint)count) return;

            _detailsBox.Text = _vm.Items[index].Details;
        }
    }

    internal class MainVm
        : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string? LogFile { get; set; }
        public ObservableCollection<LogEntry> Items { get; } = new();

        public void Reload()
        {
            var lines = File.ReadAllLines(LogFile!);
            var output = new List<LogEntry>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd();
                if (line == "") continue;

                JsonDocument parsed;
                try
                {
                    parsed = JsonDocument.Parse(line);
                }
                catch (Exception ex)
                {
                    output.Add(new LogEntry()
                    {
                        Time = "",
                        Id = "",
                        Ok = false,
                        Summary = $"Error at {i + 1}: {ex.Message}",
                        Details = $"Error at line {i + 1}\n\nException: {ex}\n\nLine: {line}",
                    });
                    continue;
                }

                string time;
                if (parsed.RootElement.TryGetProperty("time", out var timeValue))
                {
                    time = timeValue.ToString();
                }
                else
                {
                    time = "";
                }

                string id;
                if (parsed.RootElement.TryGetProperty("id", out var idValue))
                {
                    id = idValue.ToString();
                }
                else
                {
                    id = "";
                }

                output.Add(new LogEntry()
                {
                    Time = time.ToString(),
                    Id = id,
                    Summary = line,
                    Details = JsonSerializer.Serialize(parsed, new JsonSerializerOptions() { WriteIndented = true }),
                    Ok = true,
                });
            }

            output.ForEach(Items.Add);
        }
    }

    internal class LogEntry
    {
        public string? Time { get; set; }
        public string? Id { get; set; }
        public string? Summary { get; set; }
        public string? Details { get; set; }
        public bool Ok { get; set; }
    }
}

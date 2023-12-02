using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JsonLogViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
        : Window
    {
        private readonly MainVm _vm = new();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _vm;

            Loaded += MainWindow_Loaded;

            _dataGrid.Loaded += _dataGrid_Loaded;
            _dataGrid.SelectionChanged += _dataGrid_SelectionChanged;

            _statusBorder.MouseLeftButtonUp += _statusBorder_MouseLeftButtonUp;
        }

        private void _dataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = FindScrollViewer(_dataGrid);
            if (scrollViewer != null)
            {
                // Detect whether it's scrolled to the bottom;
                // https://stackoverflow.com/a/10796874
                var atBottom = true;

                scrollViewer.ScrollChanged += (_sender, _ev) =>
                {
                    atBottom = scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight;
                    //Debug.WriteLine("Scrolled to " + (atBottom ? "bottom" : "non-bottom"));
                };

                _vm.Items.CollectionChanged += (_sender, _ev) =>
                {
                    Debug.WriteLine(atBottom ? "Scrolling to bottom" : "Scroll-to-bottom skipped");
                    if (atBottom)
                    {
                        scrollViewer.ScrollToBottom();
                    }
                };
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Loaded");

            var workdir = Environment.CurrentDirectory;
            Debug.WriteLine($"workdir = {workdir}");
            {
                var d = workdir.Replace("\\", "/");
                var i = d.LastIndexOf("JsonLogViewer/JsonLogViewer");
                if (i >= 0)
                {
                    d = d[..i];
                    _vm.LogFile = $"{d}/default.log";
                    Debug.WriteLine($"log = {_vm.LogFile}");
                }
            }

            _vm.Reload();
        }

        private void _menuOpen_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Open");

            var fd = new OpenFileDialog()
            {
                CheckFileExists = true,
                Filter = "Log file|*.log;*.txt",
            };
            var ok = fd.ShowDialog() ?? false;
            if (!ok) return;

            _vm.LogFile = fd.FileName;
            _vm.Reload();
        }

        private void _dataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var index = _dataGrid.SelectedIndex;
            var count = _vm.Items.Count;
            Debug.WriteLine($"Selected [{index}/{count}]");

            if ((uint)index >= (uint)count) return;

            _detailsBox.Text = _vm.Items[index].Details;
        }

        // https://stackoverflow.com/a/7182603
        private static ScrollViewer? FindScrollViewer(DataGrid dataGrid)
        {
            var border = VisualTreeHelper.GetChild(dataGrid, 0) as Decorator;
            return border?.Child as ScrollViewer;
        }

        private void _menuTruncate_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Truncate");

            var result = MessageBox.Show($"Truncate the log file? (Removing the file contents, can't be undone.)\n\nFile: {_vm.LogFile}", "JSON Log Viewer", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _vm.Truncate();
        }

        private void _statusBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _detailsBox.Text = _vm.StatusFull;
        }
    }

    internal class MainVm
        : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void RaisePropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string? LogFile { get; set; }
        public ObservableCollection<LogEntry> Items { get; } = [];

        public string Status { get; set; } = "";
        public string StatusFull { get; set; } = "";

        private CancellationTokenSource? _currentCts;
        private Task? _currentTask;

        private async Task FollowAsync(string file, CancellationToken ct)
        {
            Debug.WriteLine($"FollowAsync file={file}");
            ShowStatus($"File: '{Path.GetFileName(file)}'", $"File: {file}");

            var index = 0;
            var entries = new List<LogEntry>();
            var last = 0L;
            var delay = 500;

            while (true)
            {
                //Debug.WriteLine($"Opening last={last}, index={index}");

                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var input = new StreamReader(stream, Encoding.UTF8))
                {
                    if (stream.Length < last)
                    {
                        Debug.WriteLine($"  File truncated: last={last}, length={stream.Length}");
                        last = 0L;
                    }
                    else
                    {
                        stream.Seek(last, SeekOrigin.Begin);
                    }

                    while (true)
                    {
                        var line = await input.ReadLineAsync(ct);
                        if (line == null)
                        {
                            //Debug.WriteLine($"  Read stopped at {stream.Position}");
                            last = stream.Position;
                            break;
                        }

                        line = line.TrimEnd();
                        if (line.Length != 0)
                        {
                            entries.Add(ParseLine(index, line));
                            index++;
                        }
                    }

                    if (entries.Count != 0)
                    {
                        entries.ForEach(Items.Add);
                        entries.Clear();
                        delay = 500;
                    }
                }

                await Task.Delay(delay, ct);
                delay = Math.Min(delay + 500, 5000);
            }
        }

        public async void Reload()
        {
            await StopWorker();

            {
                Items.Clear();
            }

            StartWorker();
        }

        public void StartWorker()
        {
            var logFile = LogFile;
            if (logFile == null) return;

            var cts = new CancellationTokenSource();
            async Task F()
            {
                try
                {
                    await FollowAsync(logFile, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (IOException ex)
                {
                    ShowStatus($"ERROR: {ex.Message}", $"File: {logFile}\n\nException: {ex}\n");
                }
#if !DEBUG
                catch (Exception ex)
                {
                    MessageBox.Show($"ERROR: {ex}", "JSON Log Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
                }
#endif
                finally
                {
                    Debug.WriteLine("Worker stopped");
                }
            }
            _currentCts = cts;
            _currentTask = F();
        }

        public async Task StopWorker()
        {
            var cts = _currentCts;
            var task = _currentTask;
            _currentCts = null;
            _currentTask = null;

            if (cts != null)
            {
                cts.Cancel();
                if (task != null)
                {
                    await task;
                }
            }
        }

        public async void Truncate()
        {
            await StopWorker();

            var logFile = LogFile;
            if (logFile == null) return;

            using (var file = new FileStream(logFile, FileMode.Truncate, FileAccess.Write, FileShare.None))
            {
                // Pass.
            }

            {
                Items.Clear();
            }

            StartWorker();
        }

        private static LogEntry ParseLine(int i, string line)
        {
            JsonDocument parsed;
            try
            {
                parsed = JsonDocument.Parse(line);
            }
            catch (Exception ex)
            {
                return new LogEntry()
                {
                    Time = "",
                    Id = "",
                    Ok = false,
                    Summary = $"Error at {i + 1}: {ex.Message}",
                    Details = $"Error at line {i + 1}\n\nException: {ex}\n\nLine: {line}",
                };
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

            return new LogEntry()
            {
                Time = time.ToString(),
                Id = id,
                Summary = line,
                Details = JsonSerializer.Serialize(parsed, new JsonSerializerOptions() { WriteIndented = true }),
                Ok = true,
            };
        }

        private void ShowStatus(string status, string full)
        {
            Status = status;
            StatusFull = full;
            RaisePropertyChanged(nameof(Status));
            RaisePropertyChanged(nameof(StatusFull));
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

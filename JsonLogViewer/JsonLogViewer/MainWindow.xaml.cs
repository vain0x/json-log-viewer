﻿using Microsoft.Win32;
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
        //private readonly DispatcherTimer _timer = new();

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

        //private void _timer_Tick(object? sender, EventArgs e)
        //{
        //    Debug.WriteLine("Tick");
        //    _vm.Reload();
        //}

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
    }

    internal class MainVm
        : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string? LogFile { get; set; }
        public ObservableCollection<LogEntry> Items { get; } = new();

        private CancellationTokenSource? _currentCts;
        private Task? _currentTask;

        private async Task FollowAsync(string file, CancellationToken ct)
        {
            Debug.WriteLine("FollowAsync file=", file);

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
            var logFile = LogFile ?? throw new Exception("no logfile specified");

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

            {
                Items.Clear();
            }

            {
                var cts = new CancellationTokenSource();
                async Task F()
                {
                    try
                    {
                        await FollowAsync(logFile, cts.Token);
                    }
#if !DEBUG
                    catch (Exception ex)
                    {
                        MessageBox.Show($"ERROR: {ex}", "JSON Log Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
#else
                    finally
                    {
                        // Pass.
                    }
#endif
                }
                _currentCts = cts;
                _currentTask = F();
            }
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

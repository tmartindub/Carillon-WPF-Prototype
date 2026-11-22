using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Carillon.WpfPrototype;

public partial class MainWindow : Window
{
    private readonly PrototypeModel model = new();
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch previewClock = new();
    private SoundPlayer? previewSound;
    private MemoryStream? previewStream;
    private double previewPosition;
    private double previewVolume = 0.35;
    private bool muted;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;
        var source = (CollectionViewSource)Resources["SongGroups"];
        source.Source = model.Songs;
        source.View.Filter = item => item is Song song && (
            song.Title.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase) ||
            song.Group.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase));
        previewTimer.Tick += (_, _) =>
        {
            var elapsed = previewPosition + previewClock.Elapsed.TotalSeconds;
            PlayerProgress.Value = elapsed;
            if (elapsed >= 3)
            {
                StopPreview();
                model.Status = "Bell preview finished.";
            }
        };
        Loaded += (_, _) => SongGrid.ScrollIntoView(model.SelectedSong);
        Closing += (_, _) => StopPreview();
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized && DataContext is not null)
            ((CollectionViewSource)Resources["SongGroups"]).View.Refresh();
    }

    private static void SetGroups(DependencyObject parent, bool expanded)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Expander expander) expander.IsExpanded = expanded;
            SetGroups(child, expanded);
        }
    }

    private void ExpandClick(object sender, RoutedEventArgs e) => SetGroups(SongGrid, true);
    private void CollapseClick(object sender, RoutedEventArgs e) => SetGroups(SongGrid, false);
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Input.Keyboard.FocusedElement is TextBox box)
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        model.Save();
    }
    private void CancelClick(object sender, RoutedEventArgs e) => model.Cancel();
    private void NavigateClick(object sender, RoutedEventArgs e)
    {
        var view = ((CollectionViewSource)Resources["SongGroups"]).View.Cast<Song>().ToList();
        if (view.Count == 0) return;
        var index = view.IndexOf(model.SelectedSong);
        index = ((sender as Button)?.Tag as string) switch
        {
            "First" => 0,
            "Last" => view.Count - 1,
            "Prior" => Math.Max(0, index - 1),
            _ => Math.Min(view.Count - 1, index + 1)
        };
        model.SelectedSong = view[index];
        SetGroups(SongGrid, true);
        SongGrid.ScrollIntoView(model.SelectedSong);
    }
    private void ToggleSchedule(object sender, RoutedEventArgs e)
    {
        model.ScheduleEnabled = ScheduleToggle.IsChecked == true;
        ScheduleToggle.Content = model.ScheduleEnabled ? "Schedule enabled" : "Schedule paused";
        model.RebuildSchedule();
        model.Status = model.ScheduleEnabled ? "Demo schedule enabled. No real playback is scheduled." : "Demo schedule paused. Editing stays available.";
    }
    private void ScheduleClick(object sender, RoutedEventArgs e) => model.ScheduleVisible = !model.ScheduleVisible;
    private void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        var warm = ThemePicker.SelectedIndex == 1;
        Application.Current.Resources["Canvas"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warm ? "#F4EEE4" : "#EFF3F6"));
        Application.Current.Resources["Surface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warm ? "#FFFCF7" : "#FFFFFF"));
        Application.Current.Resources["Line"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warm ? "#E1D7C7" : "#DCE5EB"));
    }

    private void PlayClick(object sender, RoutedEventArgs e)
    {
        if (previewTimer.IsEnabled) return;
        if (previewPosition >= 3) previewPosition = 0;
        PlayPcm(previewPosition);
        previewClock.Restart();
        previewTimer.Start();
        model.Status = "Playing the generated demonstration bell. No MP3 decoder is used.";
    }
    private void PauseClick(object sender, RoutedEventArgs e)
    {
        if (!previewTimer.IsEnabled) return;
        previewPosition += previewClock.Elapsed.TotalSeconds;
        previewTimer.Stop();
        previewClock.Reset();
        DisposeSound();
        model.Status = "Bell preview paused. Play resumes from this position.";
    }
    private void StopClick(object sender, RoutedEventArgs e)
    {
        StopPreview();
        model.Status = "Bell preview stopped.";
    }
    private void StopPreview()
    {
        previewTimer.Stop();
        previewClock.Reset();
        previewPosition = 0;
        DisposeSound();
        PlayerProgress.Value = 0;
    }
    private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        previewVolume = e.NewValue / 100;
        if (previewTimer.IsEnabled) PlayPcm(previewPosition + previewClock.Elapsed.TotalSeconds);
    }
    private void MuteClick(object sender, RoutedEventArgs e)
    {
        muted = !muted;
        MuteButton.Content = muted ? "Unmute" : "Mute";
        if (previewTimer.IsEnabled) PlayPcm(previewPosition + previewClock.Elapsed.TotalSeconds);
        model.Status = muted ? "Preview muted. Windows system volume is unchanged." : "Preview unmuted.";
    }
    private void DisposeSound()
    {
        previewSound?.Stop();
        previewSound?.Dispose();
        previewSound = null;
        previewStream?.Dispose();
        previewStream = null;
    }
    private void PlayPcm(double offset)
    {
        DisposeSound();
        if (muted || offset >= 3) return;
        const int rate = 22050;
        var count = (int)((3 - offset) * rate);
        previewStream = new MemoryStream();
        using (var writer = new BinaryWriter(previewStream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + count * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (var i = 0; i < count; i++)
            {
                var t = offset + (double)i / rate;
                var attack = Math.Min(1, t * 150);
                var decay = Math.Exp(-t * 1.55);
                var tone = Math.Sin(2 * Math.PI * 659.25 * t) +
                    0.42 * Math.Sin(2 * Math.PI * 1318.5 * t) +
                    0.19 * Math.Sin(2 * Math.PI * 1800 * t);
                writer.Write((short)(tone * decay * attack * previewVolume * 11000));
            }
        }
        previewStream.Position = 0;
        previewSound = new SoundPlayer(previewStream);
        try { previewSound.Play(); }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        { model.Status = "Audio preview unavailable on this device. The screen preview still works."; }
    }
}

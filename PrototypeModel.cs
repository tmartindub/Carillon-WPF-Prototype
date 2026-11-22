using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Carillon.WpfPrototype;

public sealed class DayChoice
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class PlayTime
{
    public string Value { get; set; } = "";
}

public sealed class Song : INotifyPropertyChanged
{
    public string Title { get; init; } = "";
    public string Group { get; init; } = "";
    public string Duration { get; init; } = "";
    private DateTime? from = DateTime.Today;
    private DateTime? to = DateTime.Today.AddYears(1);
    private string repeats = "1";
    public DateTime? From { get => from; set { from = value; Changed(); } }
    public DateTime? To { get => to; set { to = value; Changed(); } }
    public string Repeats { get => repeats; set { repeats = value; Changed(); } }
    public ObservableCollection<DayChoice> Days { get; private set; } = new(
        new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }.Select(name => new DayChoice { Name = name }));
    public ObservableCollection<PlayTime> Times { get; private set; } = new(
        Enumerable.Range(0, 12).Select(_ => new PlayTime()));

    public Song Copy()
    {
        var copy = new Song { Title = Title, Group = Group, Duration = Duration };
        copy.Restore(this);
        return copy;
    }

    public void Restore(Song source)
    {
        From = source.From;
        To = source.To;
        Repeats = source.Repeats;
        Days = new(source.Days.Select(day => new DayChoice { Name = day.Name, Enabled = day.Enabled }));
        Times = new(source.Times.Select(time => new PlayTime { Value = time.Value }));
        Changed(nameof(Days));
        Changed(nameof(Times));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class PrototypeModel : INotifyPropertyChanged
{
    public ObservableCollection<Song> Songs { get; } = [];
    private readonly Dictionary<Song, Song> saved = [];
    private Song selectedSong = null!;
    private string status = "Ready. Select a song, edit its schedule, or preview the bell.";
    private bool scheduleVisible;
    public Song SelectedSong
    {
        get => selectedSong;
        set { if (value is null) return; selectedSong = value; Changed(); }
    }
    public string Status { get => status; set { status = value; Changed(); } }
    public bool ScheduleVisible { get => scheduleVisible; set { scheduleVisible = value; Changed(); } }
    public string TodayLabel => DateTime.Today.ToString("dddd, MMMM d, yyyy", CultureInfo.GetCultureInfo("en-US")) + "  /  Main-screen concept";
    public ObservableCollection<string> ScheduleRows { get; } = [];
    public string ScheduleCountLabel => ScheduleRows.Count + " scheduled play times";
    public bool ScheduleEnabled { get; set; } = true;

    public PrototypeModel()
    {
        Add("Daily List", "Westminster Chimes - 12 O'Clock", "1:01", "12:00 PM");
        Add("Daily List", "Angelus", "1:02", "12:01 PM", "6:01 PM");
        Add("Daily List", "Let There Be Peace on Earth", "1:24", "2:00 PM");
        Add("Daily List", "Morning Has Broken", "0:51", "9:00 AM");
        Add("Daily List", "Day Is Dying in the West", "0:47", "6:00 PM");
        Add("Daily List", "Westminster Chimes - Half Hour", "0:33", "12:30 PM", "2:30 PM", "5:30 PM");
        Add("Christmas", "Angels We Have Heard on High", "1:44");
        Add("Christmas", "God Rest You Merry, Gentlemen", "2:32");
        Add("Christmas", "O Holy Night", "2:16");
        Add("Christmas", "Joy to the World!", "0:40");
        Add("Call to Services", "Triple Church Bells Calling", "1:18");
        Add("Call to Services", "Sunday Service Peal", "2:10");
        Add("Random Music", "A Mighty Fortress Is Our God", "2:28", "3:15 PM");
        Add("Random Music", "Amazing Grace", "2:31", "3:45 PM");
        Add("Random Music", "Come Thou Fount of Every Blessing", "2:53", "4:15 PM");
        SelectedSong = Songs[1];
        foreach (var song in Songs) saved[song] = song.Copy();
        RebuildSchedule();
    }

    private void Add(string group, string title, string duration, params string[] times)
    {
        var song = new Song { Group = group, Title = title, Duration = duration };
        for (var i = 0; i < times.Length; i++) song.Times[i].Value = times[i];
        Songs.Add(song);
    }

    public bool Save()
    {
        var song = SelectedSong;
        if (song.From is null || song.To is null || song.From > song.To)
        { Status = "Please choose a valid date range: From must be on or before Until."; return false; }
        if (!int.TryParse(song.Repeats, out var repeats) || repeats is < 1 or > 12)
        { Status = "Times to play must be a whole number from 1 to 12."; return false; }
        foreach (var time in song.Times.Where(time => !string.IsNullOrWhiteSpace(time.Value)))
        {
            if (!TimeOnly.TryParse(time.Value, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out _))
            { Status = "Please enter play times such as 2:05 PM or 14:05."; return false; }
        }
        saved[song] = song.Copy();
        RebuildSchedule();
        Status = "Saved in this preview. The selected song and open groups have stayed in place.";
        return true;
    }

    public void Cancel()
    {
        SelectedSong.Restore(saved[SelectedSong]);
        Status = "Edits canceled. Your selected song and open groups have stayed in place.";
    }

    public void RebuildSchedule()
    {
        ScheduleRows.Clear();
        if (!ScheduleEnabled) { Changed(nameof(ScheduleCountLabel)); return; }
        var dayIndex = ((int)DateTime.Today.DayOfWeek + 6) % 7;
        var entries = new List<(TimeOnly Time, string Song)>();
        foreach (var song in Songs)
        {
            if (song.From is null || song.To is null || song.From > DateTime.Today || song.To < DateTime.Today || !song.Days[dayIndex].Enabled)
                continue;
            foreach (var playTime in song.Times)
            {
                if (TimeOnly.TryParse(playTime.Value, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var time)
                    && !(time >= new TimeOnly(13, 0) && time < new TimeOnly(13, 30)))
                    entries.Add((time, song.Title));
            }
        }
        foreach (var entry in entries.OrderBy(entry => entry.Time))
            ScheduleRows.Add(entry.Time.ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US")) + "    " + entry.Song);
        Changed(nameof(ScheduleCountLabel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

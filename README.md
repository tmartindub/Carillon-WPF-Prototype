# Carillon WPF Main-Screen Prototype

A separate C# / WPF design study for Westminster Chimes and Carillon Bells.
The Delphi application and its files are not used or modified.

## Run

In the packaged ZIP, extract all files and open Carillon.WpfPrototype.exe.
In this source project, open bin\Release\Carillon.WpfPrototype.exe.
The prototype targets .NET 9 Desktop.
Build from the project folder with:

    dotnet build -c Release

Open Carillon.WpfPrototype.csproj in Visual Studio to edit the screen.
All visual structure, controls, spacing and styles are declared in MainWindow.xaml
and App.xaml; no controls are constructed in code.

## Try

- Select grouped songs; expand/collapse groups; search by song or group.
- Edit dates, weekdays, repetition count and up to twelve play times.
- Save or Cancel without closing the song group or losing the selected record.
- Show today's sample schedule; pause/re-enable the demo schedule.
- Switch between porcelain blue and warm parchment.
- Play, pause/resume, stop, mute and adjust the preview volume.
- Resize the window; the full design scales to fit.

All edits are in memory and reset when the prototype closes.
Sample schedules do not trigger real scheduled playback.
The player generates a three-second PCM bell; it does not play the selected MP3,
access the live database, or change system volume. The sidebar's other pages
are labels indicating possible navigation, not implemented forms.

## License

Apache License 2.0. No third-party NuGet packages are required.

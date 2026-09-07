using System.ComponentModel;
using System.Globalization;

namespace VolturaWeekNumber.Ui;

public sealed class Strings : INotifyPropertyChanged
{
    public static Strings Current { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");
    private int _column;
    private static readonly Dictionary<string, string[]> Entries = new(StringComparer.Ordinal)
    {
        ["OrdinalTab"] = ["Day of year", "Dag på året", "Tag des Jahres"],
        ["JulianTab"] = ["Julian day", "Juliansk dag", "Julianischer Tag"],
        ["OrdinalHelp"] = ["Days counted from January 1: 001–365, or 366 in a leap year.", "Dagar räknade från 1 januari: 001–365, eller 366 under skottår.", "Tage ab dem 1. Januar: 001–365, im Schaltjahr bis 366."],
        ["JulianHelp"] = ["Julian day number at noon Universal Time on the selected Gregorian date. Gregorian rules apply to all years.", "Julianskt dagnummer vid middagstid universell tid på valt gregorianskt datum. Gregorianska regler gäller för alla år.", "Julianische Tagesnummer um 12 Uhr Universalzeit am gewählten gregorianischen Datum. Gregorianische Regeln gelten für alle Jahre."],
        ["FindDate"] = ["Find a date", "Hitta ett datum", "Datum ermitteln"],
        ["Year"] = ["Year", "År", "Jahr"],
        ["Convert"] = ["Convert", "Omvandla", "Umrechnen"],
        ["InvalidOrdinal"] = ["Enter a year from 1 to 9999 and a day from 1 to 365 (366 in a leap year).", "Ange ett år från 1 till 9999 och en dag från 1 till 365 (366 under skottår).", "Jahr von 1 bis 9999 und Tag von 1 bis 365 eingeben (366 im Schaltjahr)."],
        ["InvalidJulian"] = ["Enter a whole Julian day number from 1721426 to 5373484.", "Ange ett helt julianskt dagnummer från 1721426 till 5373484.", "Eine ganze julianische Tagesnummer von 1721426 bis 5373484 eingeben."],
        ["WeekTab"] = ["Week number", "Veckonummer", "Kalenderwoche"],
        ["Preferences"] = ["Preferences", "Inställningar", "Einstellungen"],
        ["About"] = ["About", "Om", "Über"],
        ["Week"] = ["Week", "Vecka", "Woche"],
        ["ChooseDate"] = ["Choose a date", "Välj ett datum", "Datum auswählen"],
        ["Today"] = ["Today", "Idag", "Heute"],
        ["Intro"] = ["A little clarity, every week.", "Lite mer koll, varje vecka.", "Jede Woche ein wenig mehr Überblick."],
        ["Regional"] = ["Windows regional settings", "Windows regioninställningar", "Windows-Regionseinstellungen"],
        ["Iso"] = ["ISO 8601 · Monday", "ISO 8601 · måndag", "ISO 8601 · Montag"],
        ["Custom"] = ["Custom", "Anpassat", "Benutzerdefiniert"],
        ["General"] = ["Application", "Program", "Anwendung"],
        ["Language"] = ["Language", "Språk", "Sprache"],
        ["System"] = ["Follow Windows", "Följ Windows", "Windows folgen"],
        ["Theme"] = ["Window appearance", "Fönstrets utseende", "Fensterdarstellung"],
        ["Light"] = ["Light", "Ljust", "Hell"],
        ["Dark"] = ["Dark", "Mörkt", "Dunkel"],
        ["Autostart"] = ["Start with Windows", "Starta med Windows", "Mit Windows starten"],
        ["Notifications"] = ["Notifications", "Aviseringar", "Benachrichtigungen"],
        ["StartupNote"] = ["Notify when the app starts", "Avisera när programmet startar", "Beim Start benachrichtigen"],
        ["WeekNote"] = ["Notify when a new week begins", "Avisera när en ny vecka börjar", "Bei einer neuen Woche benachrichtigen"],
        ["Silent"] = ["Silent notifications", "Tysta aviseringar", "Lautlose Benachrichtigungen"],
        ["Calendar"] = ["Calendar", "Kalender", "Kalender"],
        ["Convention"] = ["Week numbering", "Veckonumrering", "Wochennummerierung"],
        ["FirstDay"] = ["First day of the week", "Veckans första dag", "Erster Wochentag"],
        ["Rule"] = ["First week of the year", "Årets första vecka", "Erste Woche des Jahres"],
        ["FirstDayRule"] = ["Contains January 1", "Innehåller 1 januari", "Enthält den 1. Januar"],
        ["FirstFullRule"] = ["First complete week", "Första hela veckan", "Erste vollständige Woche"],
        ["FirstFourRule"] = ["Contains at least four days", "Innehåller minst fyra dagar", "Enthält mindestens vier Tage"],
        ["Icon"] = ["Calendar icon", "Kalenderikon", "Kalendersymbol"],
        ["AutoIcon"] = ["Follow the Windows taskbar theme", "Följ aktivitetsfältets Windows-tema", "Windows-Taskleistendesign verwenden"],
        ["Foreground"] = ["Digits and border", "Siffror och ram", "Ziffern und Rahmen"],
        ["ChooseColor"] = ["Choose color…", "Välj färg…", "Farbe wählen…"],
        ["UseColor"] = ["Use color", "Använd färg", "Farbe übernehmen"],
        ["Opacity"] = ["Opacity", "Opacitet", "Deckkraft"],
        ["Dismiss"] = ["Cancel", "Avbryt", "Abbrechen"],
        ["Background"] = ["Calendar face", "Kalenderbakgrund", "Kalenderfläche"],
        ["ColorHelp"] = ["#AARRGGBB · AA sets opacity: 00 transparent, FF opaque.", "#AARRGGBB · AA anger opacitet: 00 transparent, FF ogenomskinlig.", "#AARRGGBB · AA bestimmt Deckkraft: 00 transparent, FF undurchsichtig."],
        ["ResetIcon"] = ["Reset icon appearance", "Återställ ikonens utseende", "Symboldarstellung zurücksetzen"],
        ["ExportIcon"] = ["Save icon…", "Spara ikon…", "Symbol speichern…"],
        ["Data"] = ["Settings and diagnostics", "Inställningar och diagnostik", "Einstellungen und Diagnose"],
        ["Logging"] = ["Enable application log", "Aktivera programlogg", "Anwendungsprotokoll aktivieren"],
        ["OpenLog"] = ["Open log", "Öppna logg", "Protokoll öffnen"],
        ["Import"] = ["Import settings…", "Importera inställningar…", "Einstellungen importieren…"],
        ["Export"] = ["Export settings…", "Exportera inställningar…", "Einstellungen exportieren…"],
        ["Save"] = ["Save changes", "Spara ändringar", "Änderungen speichern"],
        ["Saved"] = ["Settings saved.", "Inställningarna har sparats.", "Einstellungen gespeichert."],
        ["Cancel"] = ["Discard changes", "Ångra ändringar", "Änderungen verwerfen"],
        ["Updates"] = ["Updates", "Uppdateringar", "Updates"],
        ["AutoUpdates"] = ["Download verified updates automatically", "Hämta verifierade uppdateringar automatiskt", "Geprüfte Updates automatisch herunterladen"],
        ["CheckUpdates"] = ["Check for updates", "Sök efter uppdateringar", "Nach Updates suchen"],
        ["Downloads"] = ["Open downloads", "Öppna nedladdningar", "Downloads öffnen"],
        ["Install"] = ["Install and restart", "Installera och starta om", "Installieren und neu starten"],
        ["Project"] = ["Open project page", "Öppna projektsidan", "Projektseite öffnen"],
        ["License"] = ["View license", "Visa licens", "Lizenz anzeigen"],
        ["Donate"] = ["Support development", "Stöd utvecklingen", "Entwicklung unterstützen"],
        ["Coffee"] = ["Buy me a coffee", "Bjud på en kaffe", "Einen Kaffee spendieren"],
        ["Exit"] = ["Exit", "Avsluta", "Beenden"],
        ["Started"] = ["Your week number is ready in the notification area.", "Ditt veckonummer finns i meddelandefältet.", "Ihre Kalenderwoche ist im Infobereich verfügbar."],
        ["NewWeek"] = ["A new week has begun", "En ny vecka har börjat", "Eine neue Woche hat begonnen"],
        ["Hidden"] = ["Still running in the notification area. Double-click the calendar to reopen, or choose Exit to close the app.", "Programmet finns kvar i meddelandefältet. Dubbelklicka på kalendern för att öppna, eller välj Avsluta.", "Die App läuft im Infobereich weiter. Kalender doppelklicken zum Öffnen oder Beenden wählen."],
        ["Error"] = ["The operation could not be completed.", "Åtgärden kunde inte slutföras.", "Der Vorgang konnte nicht abgeschlossen werden."],
        ["Invalid"] = ["Check the date and color values before saving.", "Kontrollera datum och färgvärden innan du sparar.", "Bitte Datum und Farbwerte vor dem Speichern prüfen."],
        ["Exported"] = ["File saved.", "Filen har sparats.", "Datei gespeichert."],
        ["Imported"] = ["Settings imported.", "Inställningarna har importerats.", "Einstellungen importiert."],
        ["Checking"] = ["Checking for updates…", "Söker efter uppdateringar…", "Updates werden gesucht…"],
        ["Downloading"] = ["Downloading a verified update…", "Hämtar en verifierad uppdatering…", "Geprüftes Update wird heruntergeladen…"],
        ["Ready"] = ["An update is ready to install.", "En uppdatering är redo att installeras.", "Ein Update ist zur Installation bereit."],
        ["Current"] = ["You have the latest available version.", "Du har den senaste tillgängliga versionen.", "Sie haben die neueste verfügbare Version."],
        ["UpdateFailed"] = ["Could not verify or download an update. Try again later.", "Kunde inte verifiera eller hämta en uppdatering. Försök igen senare.", "Update konnte nicht geprüft oder heruntergeladen werden. Später erneut versuchen."],
        ["ManualUpdates"] = ["This copy uses manual updates from the project page.", "Den här kopian uppdateras manuellt från projektsidan.", "Diese Kopie wird manuell über die Projektseite aktualisiert."],
        ["SettingsRecovery"] = ["Settings could not be loaded. The original file is preserved; fix it or import a valid export before saving new settings.", "Inställningarna kunde inte läsas. Originalfilen finns kvar; rätta den eller importera en giltig export innan du sparar nya inställningar.", "Einstellungen konnten nicht geladen werden. Die Originaldatei bleibt erhalten; korrigieren Sie diese oder importieren Sie einen gültigen Export."],
        ["IsoYear"] = ["ISO week-year", "ISO-veckoår", "ISO-Wochenjahr"],
    };

    public string this[string key] => Entries.TryGetValue(key, out var values) ? values[_column] : key;
    public void SetLanguage(string language)
    {
        if (language == "system") language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        _column = language switch { "sv" => 1, "de" => 2, _ => 0 };
        Culture = CultureInfo.GetCultureInfo(_column switch { 1 => "sv-SE", 2 => "de-DE", _ => "en-GB" });
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}

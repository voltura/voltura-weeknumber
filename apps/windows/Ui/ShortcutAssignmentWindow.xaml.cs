using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Platform;

namespace VolturaWeekNumber.Ui;

public partial class ShortcutAssignmentWindow : Window
{
    private const int Escape = 0x1B;
    private const int F4 = 0x73;
    private const int Tab = 0x09;
    private const int Enter = 0x0D;
    private const int Space = 0x20;
    private readonly ActivationShortcut? _initial;
    private readonly ActivationTarget _target;
    private readonly ShortcutCaptureState _captureState;
    private readonly Func<ActivationTarget, ActivationShortcut?, bool> _isAvailable;
    private readonly Func<Func<int, bool, bool>, IShortcutCaptureHook> _hookFactory;
    private IShortcutCaptureHook? _hook;
    private bool _explicitlyCleared;

    public ShortcutAssignmentWindow(
        ActivationTarget target,
        ActivationShortcut? initial,
        Func<ActivationTarget, ActivationShortcut?, bool> isAvailable
    ) : this(target, initial, isAvailable, callback => new KeyboardShortcutCapture(callback)) { }

    internal ShortcutAssignmentWindow(
        ActivationTarget target,
        ActivationShortcut? initial,
        Func<ActivationTarget, ActivationShortcut?, bool> isAvailable,
        Func<Func<int, bool, bool>, IShortcutCaptureHook> hookFactory
    )
    {
        _target = target;
        _initial = initial;
        _captureState = new(initial);
        _isAvailable = isAvailable;
        _hookFactory = hookFactory;
        InitializeComponent();
        Language = XmlLanguage.GetLanguage(Strings.Current.Culture.Name);
        Loaded += OnLoaded;
        Activated += OnActivated;
        Deactivated += OnDeactivated;
        Closing += OnClosing;
        RefreshState();
        WindowWorkAreaPlacement.KeepVisibleAfterDisplayChanges(this);
    }

    public ActivationShortcutChange? Result { get; private set; }
    internal Exception? CaptureFailure { get; private set; }

    internal void SetReviewCandidate(ActivationShortcut shortcut)
    {
        _explicitlyCleared = false;
        _captureState.Reset(shortcut);
        RefreshState();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        WindowWorkAreaPlacement.EnsureVisibleOnCurrentMonitor(this);

        try
        {
            _hook = _hookFactory(HandleKeyChanged);
            _hook.Active = IsActive;
            _ = CaptureSurface.Focus();
        }
        catch (Exception error)
        {
            CaptureFailure = error;
            Close();
        }
    }

    private void OnActivated(object? sender, EventArgs args)
    {
        if (_hook is not null)
        {
            _hook.Active = true;
        }
    }

    private void OnDeactivated(object? sender, EventArgs args)
    {
        _captureState.ReleaseModifiers();

        if (_hook is not null)
        {
            _hook.Active = false;
        }

        RefreshState();
    }

    private bool HandleKeyChanged(int virtualKey, bool pressed)
    {
        if (!Dispatcher.CheckAccess())
        {
            return Dispatcher.Invoke(() => HandleKeyChanged(virtualKey, pressed));
        }

        if (!pressed)
        {
            _captureState.KeyUp(virtualKey);
            RefreshState();

            return true;
        }

        if (virtualKey == Escape || (virtualKey == F4 && _captureState.AltDown))
        {
            DialogResult = false;

            return true;
        }

        if (virtualKey == Tab && !_captureState.HasNonShiftModifier)
        {
            var direction = _captureState.ShiftDown
                ? FocusNavigationDirection.Previous
                : FocusNavigationDirection.Next;

            if (Keyboard.FocusedElement is UIElement focused)
            {
                _ = focused.MoveFocus(new TraversalRequest(direction));
            }

            return true;
        }

        if (
            (virtualKey is Enter or Space)
            && !_captureState.HasNonShiftModifier
            && !_captureState.ShiftDown
            && Keyboard.FocusedElement is System.Windows.Controls.Button
        )
        {
            return false;
        }

        _explicitlyCleared = false;
        _captureState.KeyDown(virtualKey);
        RefreshState();

        return true;
    }

    private void CaptureSurfaceClick(object sender, MouseButtonEventArgs args) =>
        _ = CaptureSurface.Focus();

    private void ResetClick(object sender, RoutedEventArgs args)
    {
        _explicitlyCleared = false;
        _captureState.Reset(_initial);
        RefreshState();
        _ = CaptureSurface.Focus();
    }

    private void ClearClick(object sender, RoutedEventArgs args)
    {
        _explicitlyCleared = true;
        _captureState.Clear();
        RefreshState();
        _ = CaptureSurface.Focus();
    }

    private void SaveClick(object sender, RoutedEventArgs args) => Save();

    private void Save()
    {
        if (!SaveButton.IsEnabled)
        {
            return;
        }

        Result = new(_target, _captureState.Candidate);
        DialogResult = true;
    }

    private void RefreshState()
    {
        var candidate = _captureState.Candidate;
        var complete = candidate is { IsValid: true };
        var invalid = candidate is { IsValid: false };
        var conflict = complete && !_isAvailable(_target, candidate);
        var cleared = candidate is null && _explicitlyCleared && _initial is not null;

        KeyTiles.ItemsSource = null;
        KeyTiles.ItemsSource = _captureState.Preview;
        KeyTiles.Tag = invalid;
        System.Windows.Automation.AutomationProperties.SetName(
            KeyTiles,
            ShortcutDisplay.Text(_captureState.Preview)
        );
        PromptText.Visibility = _captureState.Preview.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        KeyTiles.Visibility = _captureState.Preview.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        ClearButton.Visibility = _captureState.Preview.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        ConflictBanner.Visibility = conflict
            ? Visibility.Visible
            : Visibility.Collapsed;
        InvalidBanner.Visibility = invalid
            ? Visibility.Visible
            : Visibility.Collapsed;
        SaveButton.IsEnabled = cleared || (candidate != _initial && complete && !conflict);
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        _hook?.Dispose();
        _hook = null;
    }
}

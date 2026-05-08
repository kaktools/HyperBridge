using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using HyperBridge.App.Infrastructure;
using HyperBridge.Core.Contracts;
using HyperBridge.Core.Enums;
using HyperBridge.Core.Models;
using HyperBridge.Services;

namespace HyperBridge.App.ViewModels;

public enum AppPane
{
    Dashboard,
    Wizard,
    Logs,
    Settings,
    Info,
}

public sealed class MainViewModel : ViewModelBase
{
    private const int MbPerGb = 1024;
    private const string RepositoryUrl = "https://github.com/kaktools/HyperBridge";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/kaktools/HyperBridge/releases/latest";
    private static readonly HttpClient UpdateHttpClient = CreateUpdateHttpClient();

    private readonly IVirtualBoxService _virtualBoxService;
    private readonly IHyperVService _hyperVService;
    private readonly IMigrationService _migrationService;
    private readonly ISystemCheckService _systemCheckService;
    private readonly ILoggingService _loggingService;
    private readonly IReportService _reportService;
    private readonly ISettingsService _settingsService;

    private AppSettings _settings = new();
    private AppPane _currentPane;
    private int _wizardStep = 1;
    private string _vmFilter = string.Empty;
    private bool _isBusy;
    private int _progressPercent;
    private string _currentStepText = "Bereit";
    private string _technicalDetails = string.Empty;
    private string _compatibilitySummary = "Noch nicht berechnet";
    private string _compatibilityReasons = string.Empty;
    private string _compatibilityActions = string.Empty;
    private CompatibilityLevel _compatibilityLevel = CompatibilityLevel.Yellow;
    private string _lastReportPath = string.Empty;
    private string _reportOutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HyperBridge", "Reports");
    private string _notes = string.Empty;
    private bool _hostnameChanged;
    private bool _localUsersReviewed;
    private bool _userPasswordsChanged;
    private bool _adminPasswordChanged;
    private bool _networkParamsDocumented;
    private bool _guestAdditionsHandled;
    private bool _guestShutdownCleanly;
    private string _hyperVVmName = string.Empty;
    private string _targetPath = string.Empty;
    private int _generation = 2;
    private int _memoryMb = 4096;
    private bool _dynamicMemoryEnabled = true;
    private int _startupMemoryMb = 4096;
    private int _minimumMemoryMb = 2048;
    private int _maximumMemoryMb = 8192;
    private int _cpuCount = 2;
    private string _selectedSwitch = string.Empty;
    private bool _secureBootEnabled = true;
    private bool _startAfterCreation;
    private bool _createCheckpoint;
    private bool _dryRun;
    private VirtualMachineSummary? _selectedVm;
    private VirtualMachineAnalysis? _analysis;
    private SystemStatus _systemStatus = new();
    private string _updateStatusText = "Update-Check ausstehend";
    private CancellationTokenSource? _migrationCts;

    public MainViewModel(
        IVirtualBoxService virtualBoxService,
        IHyperVService hyperVService,
        IMigrationService migrationService,
        ISystemCheckService systemCheckService,
        ILoggingService loggingService,
        IReportService reportService,
        ISettingsService settingsService)
    {
        _virtualBoxService = virtualBoxService;
        _hyperVService = hyperVService;
        _migrationService = migrationService;
        _systemCheckService = systemCheckService;
        _loggingService = loggingService;
        _reportService = reportService;
        _settingsService = settingsService;

        AvailableVms = [];
        FilteredVms = [];
        Switches = [];
        LiveLogEntries = [];

        NavigateCommand = new RelayCommand(OnNavigate);
        StartNewMigrationCommand = new RelayCommand(_ => StartNewMigration(false), _ => !IsBusy && CanStartMigrationProcess);
        StartDryRunCommand = new RelayCommand(_ => StartNewMigration(true), _ => !IsBusy && CanStartMigrationProcess);
        RefreshStatusCommand = new AsyncRelayCommand(_ => RefreshStatusAsync());
        CheckForUpdatesCommand = new AsyncRelayCommand(_ => CheckForUpdatesAsync(showDialogs: true), _ => !IsBusy);
        LoadVmsCommand = new AsyncRelayCommand(_ => LoadVirtualMachinesAsync(), _ => !IsBusy);
        AnalyzeVmCommand = new AsyncRelayCommand(_ => AnalyzeVmAsync(), _ => !IsBusy);
        LoadSwitchesCommand = new AsyncRelayCommand(_ => LoadSwitchesAsync(), _ => !IsBusy);
        EvaluateCompatibilityCommand = new RelayCommand(_ => EvaluateCompatibility(), _ => Analysis is not null);
        RunPreCheckCommand = new AsyncRelayCommand(_ => RunPreCheckAsync(), _ => Analysis is not null && !IsBusy);
        StartMigrationCommand = new AsyncRelayCommand(_ => StartMigrationAsync(), _ => CanStartMigration());
        CancelMigrationCommand = new RelayCommand(_ => CancelMigration(), _ => IsBusy);
        NextStepCommand = new RelayCommand(_ => MoveNextStep(), _ => WizardStep < 7);
        PreviousStepCommand = new RelayCommand(_ => WizardStep = Math.Max(1, WizardStep - 1), _ => WizardStep > 1);
        BrowseTargetFolderCommand = new RelayCommand(_ => BrowseTargetFolder());
        OpenLogCommand = new RelayCommand(_ => OpenPath(_loggingService.CurrentLogPath), _ => File.Exists(_loggingService.CurrentLogPath));
        OpenTargetFolderCommand = new RelayCommand(_ => OpenPath(TargetPath), _ => Directory.Exists(TargetPath));
        OpenHyperVManagerCommand = new RelayCommand(_ => OpenHyperVManager());
        ExportReportCommand = new AsyncRelayCommand(_ => ExportReportAsync(), _ => !IsBusy && CanExportReport());
        CopyLogsCommand = new RelayCommand(_ => CopyLogsToClipboard());
        CopyTechnicalDetailsCommand = new RelayCommand(_ => CopyTechnicalDetailsToClipboard());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());

        _loggingService.LogAdded += OnLogAdded;
    }

    public ObservableCollection<VirtualMachineSummary> AvailableVms { get; }

    public ObservableCollection<VirtualMachineSummary> FilteredVms { get; }

    public ObservableCollection<string> Switches { get; }

    public ObservableCollection<string> LiveLogEntries { get; }

    public RelayCommand NavigateCommand { get; }

    public RelayCommand StartNewMigrationCommand { get; }

    public RelayCommand StartDryRunCommand { get; }

    public AsyncRelayCommand RefreshStatusCommand { get; }

    public AsyncRelayCommand CheckForUpdatesCommand { get; }

    public AsyncRelayCommand LoadVmsCommand { get; }

    public AsyncRelayCommand AnalyzeVmCommand { get; }

    public AsyncRelayCommand LoadSwitchesCommand { get; }

    public RelayCommand EvaluateCompatibilityCommand { get; }

    public AsyncRelayCommand RunPreCheckCommand { get; }

    public AsyncRelayCommand StartMigrationCommand { get; }

    public RelayCommand CancelMigrationCommand { get; }

    public RelayCommand NextStepCommand { get; }

    public RelayCommand PreviousStepCommand { get; }

    public RelayCommand BrowseTargetFolderCommand { get; }

    public RelayCommand OpenLogCommand { get; }

    public RelayCommand OpenTargetFolderCommand { get; }

    public RelayCommand OpenHyperVManagerCommand { get; }

    public AsyncRelayCommand ExportReportCommand { get; }

    public RelayCommand CopyLogsCommand { get; }

    public RelayCommand CopyTechnicalDetailsCommand { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public string FooterPrimaryButtonText => WizardStep == 6 ? "Migration starten" : "Weiter";

    public bool IsFooterPrimaryButtonVisible => WizardStep < 7;

    public System.Windows.Input.ICommand FooterPrimaryCommand => WizardStep == 6
        ? StartMigrationCommand
        : NextStepCommand;

    public AppPane CurrentPane
    {
        get => _currentPane;
        set => SetProperty(ref _currentPane, value);
    }

    public int WizardStep
    {
        get => _wizardStep;
        set
        {
            if (SetProperty(ref _wizardStep, value))
            {
                PreviousStepCommand.RaiseCanExecuteChanged();
                NextStepCommand.RaiseCanExecuteChanged();
                StartMigrationCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(FooterPrimaryButtonText));
                OnPropertyChanged(nameof(IsFooterPrimaryButtonVisible));
                OnPropertyChanged(nameof(FooterPrimaryCommand));
            }
        }
    }

    public string VmFilter
    {
        get => _vmFilter;
        set
        {
            if (SetProperty(ref _vmFilter, value))
            {
                ApplyVmFilter();
            }
        }
    }

    public VirtualMachineSummary? SelectedVm
    {
        get => _selectedVm;
        set
        {
            if (SetProperty(ref _selectedVm, value) && value is not null)
            {
                HyperVVmName = $"{value.Name}-HV";
                ApplyVmResourceDefaults(value);

                if (string.IsNullOrWhiteSpace(TargetPath))
                {
                    TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HyperBridge", value.Name);
                }
            }

            NextStepCommand.RaiseCanExecuteChanged();
        }
    }

    public VirtualMachineAnalysis? Analysis
    {
        get => _analysis;
        private set
        {
            if (SetProperty(ref _analysis, value))
            {
                NextStepCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public SystemStatus SystemStatus
    {
        get => _systemStatus;
        private set
        {
            if (SetProperty(ref _systemStatus, value))
            {
                OnPropertyChanged(nameof(IsAdmin));
                OnPropertyChanged(nameof(IsVirtualBoxDetected));
                OnPropertyChanged(nameof(IsHyperVDetected));
                OnPropertyChanged(nameof(IsVBoxManageDetected));
                OnPropertyChanged(nameof(IsHyperVPowerShellDetected));
                OnPropertyChanged(nameof(CanStartMigrationProcess));
                StartNewMigrationCommand.RaiseCanExecuteChanged();
                StartDryRunCommand.RaiseCanExecuteChanged();
                StartMigrationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsAdmin => SystemStatus.IsAdmin;

    public bool IsVirtualBoxDetected => SystemStatus.IsVirtualBoxInstalled;

    public bool IsHyperVDetected => SystemStatus.IsHyperVAvailable;

    public bool IsVBoxManageDetected => SystemStatus.IsVBoxManageAvailable;

    public bool IsHyperVPowerShellDetected => SystemStatus.IsHyperVPowerShellAvailable;

    public bool CanStartMigrationProcess =>
        IsAdmin
        && IsVirtualBoxDetected
        && IsHyperVDetected
        && IsVBoxManageDetected
        && IsHyperVPowerShellDetected;

    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set => SetProperty(ref _updateStatusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CancelMigrationCommand.RaiseCanExecuteChanged();
                StartMigrationCommand.RaiseCanExecuteChanged();
                LoadVmsCommand.RaiseCanExecuteChanged();
                AnalyzeVmCommand.RaiseCanExecuteChanged();
                RunPreCheckCommand.RaiseCanExecuteChanged();
                ExportReportCommand.RaiseCanExecuteChanged();
                StartNewMigrationCommand.RaiseCanExecuteChanged();
                StartDryRunCommand.RaiseCanExecuteChanged();
                CheckForUpdatesCommand.RaiseCanExecuteChanged();
                NextStepCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string CurrentStepText
    {
        get => _currentStepText;
        private set => SetProperty(ref _currentStepText, value);
    }

    public string TechnicalDetails
    {
        get => _technicalDetails;
        private set => SetProperty(ref _technicalDetails, value);
    }

    public string CompatibilitySummary
    {
        get => _compatibilitySummary;
        private set => SetProperty(ref _compatibilitySummary, value);
    }

    public string CompatibilityReasons
    {
        get => _compatibilityReasons;
        private set => SetProperty(ref _compatibilityReasons, value);
    }

    public string CompatibilityActions
    {
        get => _compatibilityActions;
        private set => SetProperty(ref _compatibilityActions, value);
    }

    public CompatibilityLevel CompatibilityLevel
    {
        get => _compatibilityLevel;
        private set => SetProperty(ref _compatibilityLevel, value);
    }

    public string ReportOutputFolder
    {
        get => _reportOutputFolder;
        set => SetProperty(ref _reportOutputFolder, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public bool HostnameChanged
    {
        get => _hostnameChanged;
        set => SetProperty(ref _hostnameChanged, value);
    }

    public bool LocalUsersReviewed
    {
        get => _localUsersReviewed;
        set => SetProperty(ref _localUsersReviewed, value);
    }

    public bool UserPasswordsChanged
    {
        get => _userPasswordsChanged;
        set => SetProperty(ref _userPasswordsChanged, value);
    }

    public bool AdminPasswordChanged
    {
        get => _adminPasswordChanged;
        set => SetProperty(ref _adminPasswordChanged, value);
    }

    public bool NetworkParamsDocumented
    {
        get => _networkParamsDocumented;
        set => SetProperty(ref _networkParamsDocumented, value);
    }

    public bool GuestAdditionsHandled
    {
        get => _guestAdditionsHandled;
        set => SetProperty(ref _guestAdditionsHandled, value);
    }

    public bool GuestShutdownCleanly
    {
        get => _guestShutdownCleanly;
        set => SetProperty(ref _guestShutdownCleanly, value);
    }

    public string HyperVVmName
    {
        get => _hyperVVmName;
        set
        {
            if (SetProperty(ref _hyperVVmName, value))
            {
                NextStepCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string TargetPath
    {
        get => _targetPath;
        set
        {
            if (SetProperty(ref _targetPath, value))
            {
                NextStepCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int Generation
    {
        get => _generation;
        set => SetProperty(ref _generation, value);
    }

    public int MemoryMb
    {
        get => _memoryMb;
        set
        {
            if (SetProperty(ref _memoryMb, value))
            {
                OnPropertyChanged(nameof(MemoryGb));
            }
        }
    }

    public int MemoryGb
    {
        get => GbFromMb(MemoryMb);
        set => MemoryMb = MbFromGb(value);
    }

    public bool DynamicMemoryEnabled
    {
        get => _dynamicMemoryEnabled;
        set => SetProperty(ref _dynamicMemoryEnabled, value);
    }

    public int StartupMemoryMb
    {
        get => _startupMemoryMb;
        set
        {
            if (SetProperty(ref _startupMemoryMb, value))
            {
                OnPropertyChanged(nameof(StartupMemoryGb));
            }
        }
    }

    public int StartupMemoryGb
    {
        get => GbFromMb(StartupMemoryMb);
        set => StartupMemoryMb = MbFromGb(value);
    }

    public int MinimumMemoryMb
    {
        get => _minimumMemoryMb;
        set
        {
            if (SetProperty(ref _minimumMemoryMb, value))
            {
                OnPropertyChanged(nameof(MinimumMemoryGb));
            }
        }
    }

    public int MinimumMemoryGb
    {
        get => GbFromMb(MinimumMemoryMb);
        set => MinimumMemoryMb = MbFromGb(value);
    }

    public int MaximumMemoryMb
    {
        get => _maximumMemoryMb;
        set
        {
            if (SetProperty(ref _maximumMemoryMb, value))
            {
                OnPropertyChanged(nameof(MaximumMemoryGb));
            }
        }
    }

    public int MaximumMemoryGb
    {
        get => GbFromMb(MaximumMemoryMb);
        set => MaximumMemoryMb = MbFromGb(value);
    }

    public int CpuCount
    {
        get => _cpuCount;
        set => SetProperty(ref _cpuCount, value);
    }

    public string SelectedSwitch
    {
        get => _selectedSwitch;
        set => SetProperty(ref _selectedSwitch, value);
    }

    public bool SecureBootEnabled
    {
        get => _secureBootEnabled;
        set => SetProperty(ref _secureBootEnabled, value);
    }

    public bool StartAfterCreation
    {
        get => _startAfterCreation;
        set => SetProperty(ref _startAfterCreation, value);
    }

    public bool CreateCheckpoint
    {
        get => _createCheckpoint;
        set => SetProperty(ref _createCheckpoint, value);
    }

    public bool DryRun
    {
        get => _dryRun;
        set => SetProperty(ref _dryRun, value);
    }

    public string LastReportPath => _lastReportPath;

    public async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync(CancellationToken.None).ConfigureAwait(false);

        DryRun = _settings.LastDryRun;
        TargetPath = _settings.LastTargetPath;
        SelectedSwitch = _settings.LastVirtualSwitch;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await RefreshStatusAsync().ConfigureAwait(false);
            await LoadSwitchesAsync().ConfigureAwait(false);
            await LoadVirtualMachinesAsync().ConfigureAwait(false);

            CurrentPane = AppPane.Wizard;
            WizardStep = 1;
        });

        _ = CheckForUpdatesAsync(showDialogs: false);

        _settings.Theme = "Dark";
    }

    public event EventHandler<string>? ThemeRequested;

    private void OnNavigate(object? parameter)
    {
        if (parameter is not string target || !Enum.TryParse<AppPane>(target, out var pane))
        {
            return;
        }

        CurrentPane = pane;
    }

    private void StartNewMigration(bool dryRun)
    {
        if (IsBusy)
        {
            return;
        }

        if (!CanStartMigrationProcess)
        {
            CurrentStepText = BuildPrerequisitesStatusMessage();
            return;
        }

        DryRun = dryRun;
        WizardStep = 1;
        ProgressPercent = 0;
        CurrentStepText = dryRun
            ? "Dry Run vorbereitet"
            : "Neue Migration vorbereitet";
        TechnicalDetails = string.Empty;
        Analysis = null;
        CompatibilitySummary = "Noch nicht berechnet";
        CompatibilityReasons = string.Empty;
        CompatibilityActions = string.Empty;
        SelectedVm = null;
        _lastReportPath = string.Empty;
        OnPropertyChanged(nameof(LastReportPath));
        ExportReportCommand.RaiseCanExecuteChanged();
        CurrentPane = AppPane.Wizard;
    }

    private bool CanMoveToNextStep()
    {
        return WizardStep switch
        {
            1 => SelectedVm is not null,
            2 => Analysis is not null,
            3 => true,
            4 => !string.IsNullOrWhiteSpace(HyperVVmName) && !string.IsNullOrWhiteSpace(TargetPath),
            5 => true,
            6 => false,
            _ => false,
        };
    }

    private void MoveNextStep()
    {
        if (WizardStep >= 7)
        {
            return;
        }

        if (!CanMoveToNextStep())
        {
            CurrentStepText = WizardStep switch
            {
                1 => "Bitte zuerst eine VM auswählen",
                2 => "Bitte zuerst auf 'VM analysieren' klicken",
                4 => "Bitte Hyper-V-Name und Zielpfad ausfüllen",
                6 => "Starten Sie die Migration über 'Migration starten'",
                _ => "Bitte erforderliche Eingaben prüfen",
            };
            return;
        }

        if (WizardStep == 1)
        {
            WizardStep = 2;

            // Analysis should start automatically when entering step 2.
            if (SelectedVm is not null && (Analysis is null || !string.Equals(Analysis.Summary.Name, SelectedVm.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _ = AnalyzeVmAsync();
            }

            return;
        }

        WizardStep = Math.Min(7, WizardStep + 1);
    }

    private async Task RefreshStatusAsync()
    {
        await RunBusyAsync(async token =>
        {
            CurrentStepText = "Systemstatus wird geprüft";
            SystemStatus = await _systemCheckService.GetSystemStatusAsync(token).ConfigureAwait(false);

            var prerequisitesMessage = BuildPrerequisitesStatusMessage();
            CurrentStepText = prerequisitesMessage;

            if (!CanStartMigrationProcess)
            {
                _loggingService.LogWarning($"Migration aktuell gesperrt: {prerequisitesMessage}");
            }

            _loggingService.LogInfo("Systemstatus aktualisiert.");
        }).ConfigureAwait(false);
    }

    private async Task CheckForUpdatesAsync(bool showDialogs)
    {
        var currentVersion = GetCurrentAppVersion();
        var currentVersionText = FormatVersionForDisplay(currentVersion);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await UpdateHttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

            var root = json.RootElement;
            var latestTag = root.TryGetProperty("tag_name", out var tagProp)
                ? tagProp.GetString() ?? string.Empty
                : string.Empty;
            var latestUrl = root.TryGetProperty("html_url", out var urlProp)
                ? urlProp.GetString() ?? RepositoryUrl
                : RepositoryUrl;

            if (!TryParseVersion(latestTag, out var latestVersion))
            {
                UpdateStatusText = "Update-Check: Release-Version konnte nicht gelesen werden";
                if (showDialogs)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show(
                            "Die aktuelle Release-Version aus GitHub konnte nicht ausgewertet werden.",
                            "Update-Check",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning));
                }

                return;
            }

            if (latestVersion > currentVersion)
            {
                var latestVersionText = FormatVersionForDisplay(latestVersion);
                UpdateStatusText = $"Update verfügbar: v{latestVersionText}";
                _loggingService.LogInfo($"Update verfügbar: aktuell v{currentVersionText}, GitHub v{latestVersionText}");

                if (showDialogs)
                {
                    var openRelease = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show(
                            $"Neue Version gefunden:{Environment.NewLine}Installiert: v{currentVersionText}{Environment.NewLine}GitHub: v{latestVersionText}{Environment.NewLine}{Environment.NewLine}Release-Seite jetzt öffnen?",
                            "Update verfügbar",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information) == MessageBoxResult.Yes);

                    if (openRelease)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = latestUrl,
                            UseShellExecute = true,
                        });
                    }
                }

                return;
            }

            UpdateStatusText = $"Aktuell: v{currentVersionText}";

            if (showDialogs)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(
                        $"Du nutzt bereits die aktuelle Version (v{currentVersionText}).",
                        "Update-Check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information));
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = "Update-Check fehlgeschlagen";
            _loggingService.LogWarning($"Update-Check fehlgeschlagen: {ex.Message}");

            if (showDialogs)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(
                        $"Der Update-Check konnte nicht durchgeführt werden:{Environment.NewLine}{ex.Message}",
                        "Update-Check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning));
            }
        }
    }

    private async Task LoadVirtualMachinesAsync()
    {
        await RunBusyAsync(async token =>
        {
            CurrentStepText = "VirtualBox-VMs werden geladen";
            var list = await _virtualBoxService.GetVirtualMachinesAsync(token).ConfigureAwait(false);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AvailableVms.Clear();
                foreach (var vm in list)
                {
                    AvailableVms.Add(vm);
                }

                ApplyVmFilter();
                if (!string.IsNullOrWhiteSpace(_settings.LastSelectedVmName))
                {
                    SelectedVm = AvailableVms.FirstOrDefault(v => string.Equals(v.Name, _settings.LastSelectedVmName, StringComparison.OrdinalIgnoreCase));
                }
            });

            _loggingService.LogInfo($"{list.Count} VirtualBox-VM(s) geladen.");
        }).ConfigureAwait(false);
    }

    private async Task AnalyzeVmAsync()
    {
        if (SelectedVm is null)
        {
            CurrentStepText = "Bitte zuerst eine VM in der Liste auswählen";
            _loggingService.LogWarning("Analyse angefordert ohne VM-Auswahl.");
            return;
        }

        await RunBusyAsync(async token =>
        {
            CurrentStepText = "VM wird analysiert";
            var analysis = await _virtualBoxService.AnalyzeVmAsync(SelectedVm.Name, TargetPath, token).ConfigureAwait(false);
            Analysis = analysis;
            TechnicalDetails = JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true });
            EvaluateCompatibility();
            WizardStep = Math.Max(WizardStep, 2);
            _loggingService.LogInfo($"Analyse für VM '{SelectedVm.Name}' abgeschlossen.");
        }).ConfigureAwait(false);
    }

    private async Task LoadSwitchesAsync()
    {
        await RunBusyAsync(async token =>
        {
            var switches = await _hyperVService.GetVirtualSwitchesAsync(token).ConfigureAwait(false);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Switches.Clear();
                foreach (var item in switches)
                {
                    Switches.Add(item);
                }

                if (!string.IsNullOrWhiteSpace(SelectedSwitch) && !Switches.Contains(SelectedSwitch))
                {
                    SelectedSwitch = string.Empty;
                }
            });
        }).ConfigureAwait(false);
    }

    private void EvaluateCompatibility()
    {
        if (Analysis is null)
        {
            return;
        }

        var assessment = CompatibilityAdvisor.Evaluate(Analysis);
        CompatibilityLevel = assessment.Level;
        CompatibilitySummary = assessment.Recommendation;
        CompatibilityReasons = string.Join(Environment.NewLine, assessment.Reasons.Select(r => $"- {r}"));
        CompatibilityActions = string.Join(Environment.NewLine, assessment.Actions.Select(r => $"- {r}"));

        if (Generation != assessment.SuggestedGeneration)
        {
            Generation = assessment.SuggestedGeneration;
        }
    }

    private async Task RunPreCheckAsync()
    {
        if (Analysis is null)
        {
            return;
        }

        await RunBusyAsync(async token =>
        {
            var request = BuildMigrationRequest(Analysis);
            var preChecks = await _migrationService.RunPreChecksAsync(request, token).ConfigureAwait(false);

            var checkText = preChecks.Issues.Count == 0
                ? "Pre-Check erfolgreich. Keine kritischen Probleme erkannt."
                : string.Join(Environment.NewLine + Environment.NewLine, preChecks.Issues.Select(i =>
                    $"[{i.Severity}] {i.Title}{Environment.NewLine}Detail: {i.TechnicalDetail}{Environment.NewLine}Ursache: {i.PossibleCause}{Environment.NewLine}Nächster Schritt: {i.NextStep}"));

            TechnicalDetails = checkText;
            _loggingService.LogInfo(preChecks.CanProceed ? "Pre-Check erfolgreich." : "Pre-Check hat Probleme erkannt.");
            WizardStep = Math.Max(WizardStep, 6);
        }).ConfigureAwait(false);
    }

    private async Task StartMigrationAsync()
    {
        if (Analysis is null)
        {
            return;
        }

        _migrationCts = new CancellationTokenSource();

        await RunBusyAsync(async _ =>
        {
            CurrentStepText = "Migration läuft";
            ProgressPercent = 0;
            WizardStep = 6;

            var request = BuildMigrationRequest(Analysis);
            var progress = new Progress<MigrationProgressUpdate>(update =>
            {
                ProgressPercent = update.Percent;
                CurrentStepText = $"{update.Step}: {update.Message}";
                if (!string.IsNullOrWhiteSpace(update.TechnicalDetails))
                {
                    TechnicalDetails = update.TechnicalDetails;
                }
                if (!string.IsNullOrWhiteSpace(update.Message))
                {
                    _loggingService.Log(update.Level, update.Message);
                }
            });

            var result = await _migrationService.ExecuteMigrationAsync(request, progress, _migrationCts.Token).ConfigureAwait(false);
            WizardStep = 7;
            CurrentStepText = result.Summary;

            var cleanupNote = await PromptDeleteVhdAfterSuccessfulMigrationAsync(result).ConfigureAwait(false);

            var configJson = JsonSerializer.Serialize(BuildTargetConfiguration(), new JsonSerializerOptions { WriteIndented = true });
            var sourceDisks = Analysis.DiskPaths.Count > 0 ? Analysis.DiskPaths : [Analysis.DiskPath];
            var targetVhdxDisks = result.VhdxPaths.Count > 0 ? result.VhdxPaths : [result.VhdxPath];
            var reportData = new ReportData
            {
                SourceVm = SelectedVm?.Name ?? string.Empty,
                SourceDisk = string.Join(Environment.NewLine, sourceDisks.Where(path => !string.IsNullOrWhiteSpace(path))),
                TargetVm = result.HyperVVmName,
                TargetVhdx = string.Join(Environment.NewLine, targetVhdxDisks.Where(path => !string.IsNullOrWhiteSpace(path))),
                StartedAtUtc = result.StartedAtUtc,
                FinishedAtUtc = result.FinishedAtUtc,
                ResultSummary = result.Summary,
                ConfigurationJson = configJson,
                Warnings = result.Issues.Where(i => i.Severity == CheckSeverity.Warning).Select(i => i.Title).ToList(),
                Errors = result.Issues.Where(i => i.Severity == CheckSeverity.Error).Select(i => i.Title).ToList(),
                LogExcerpt = _loggingService.GetRecentEntries(300),
            };

            Directory.CreateDirectory(ReportOutputFolder);
            _lastReportPath = await _reportService.GenerateHtmlReportAsync(reportData, ReportOutputFolder, CancellationToken.None).ConfigureAwait(false);
            await _reportService.GenerateTextReportAsync(reportData, ReportOutputFolder, CancellationToken.None).ConfigureAwait(false);
            OnPropertyChanged(nameof(LastReportPath));
            ExportReportCommand.RaiseCanExecuteChanged();

            var finalLines = new List<string>
            {
                $"Status: {result.Status}",
                $"VHD-Dateien: {string.Join(", ", result.VhdPaths.Count > 0 ? result.VhdPaths : [result.VhdPath])}",
                $"VHDX-Dateien: {string.Join(", ", result.VhdxPaths.Count > 0 ? result.VhdxPaths : [result.VhdxPath])}",
                $"Hyper-V VM: {result.HyperVVmName}",
                $"Report: {_lastReportPath}",
            };

            if (!string.IsNullOrWhiteSpace(cleanupNote))
            {
                finalLines.Add(cleanupNote);
            }

            if (result.Issues.Count > 0)
            {
                finalLines.Add("Hinweise:");
                finalLines.AddRange(result.Issues.Select(i => $"- [{i.Severity}] {i.Title}: {i.NextStep}"));
            }

            TechnicalDetails = string.Join(Environment.NewLine, finalLines);
            await SaveSettingsAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private async Task<string> PromptDeleteVhdAfterSuccessfulMigrationAsync(MigrationResult result)
    {
        if (result.Status is not (MigrationStatus.Success or MigrationStatus.PartialSuccess))
        {
            return string.Empty;
        }

        var vhdPaths = (result.VhdPaths.Count > 0 ? result.VhdPaths : [result.VhdPath])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var vhdxPaths = (result.VhdxPaths.Count > 0 ? result.VhdxPaths : [result.VhdxPath])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (vhdPaths.Count == 0 || vhdxPaths.Count == 0)
        {
            return string.Empty;
        }

        var existingVhdPaths = vhdPaths.Where(File.Exists).ToList();
        if (existingVhdPaths.Count == 0 || !vhdxPaths.Any(File.Exists))
        {
            return string.Empty;
        }

        var listText = string.Join(Environment.NewLine, existingVhdPaths.Select(path => $"- VHD: {path}"));
        var askDelete = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            MessageBox.Show(
                $"Die Migration war erfolgreich und es wurden {existingVhdPaths.Count} temporäre VHD-Datei(en) gefunden:{Environment.NewLine}{Environment.NewLine}{listText}{Environment.NewLine}{Environment.NewLine}Diese VHD-Dateien werden nach der Konvertierung normalerweise nicht mehr benötigt. Möchtest du sie jetzt löschen?",
                "VHDs nach Migration löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes);

        if (!askDelete)
        {
            _loggingService.LogInfo($"VHD-Dateien beibehalten: {string.Join(", ", existingVhdPaths)}");
            return "VHD-Dateien beibehalten: Benutzer hat Löschen abgelehnt.";
        }

        var deletedCount = 0;
        var failedDeletes = new List<string>();
        foreach (var vhdPath in existingVhdPaths)
        {
            try
            {
                File.Delete(vhdPath);
                deletedCount++;
                _loggingService.LogInfo($"VHD gelöscht: {vhdPath}");
            }
            catch (Exception ex)
            {
                failedDeletes.Add($"{vhdPath} ({ex.Message})");
                _loggingService.LogWarning($"VHD konnte nicht gelöscht werden: {vhdPath}. Fehler: {ex.Message}");
            }
        }

        if (failedDeletes.Count > 0)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    $"Nicht alle VHD-Dateien konnten gelöscht werden:{Environment.NewLine}{string.Join(Environment.NewLine, failedDeletes)}",
                    "VHDs löschen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning));
            return $"VHD-Löschung teilweise fehlgeschlagen ({deletedCount}/{existingVhdPaths.Count} gelöscht).";
        }

        return $"VHDs gelöscht: {deletedCount} Datei(en) entfernt.";
    }

    private void CancelMigration()
    {
        _migrationCts?.Cancel();
        _loggingService.LogWarning("Migrationsabbruch angefordert.");
    }

    private void BrowseTargetFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Zielordner für die Hyper-V-Migration auswählen",
            InitialDirectory = string.IsNullOrWhiteSpace(TargetPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : TargetPath,
        };

        if (dialog.ShowDialog() == true)
        {
            TargetPath = dialog.FolderName;
        }
    }

    private async Task ExportReportAsync()
    {
        if (!CanExportReport())
        {
            _loggingService.LogWarning("Kein Report verfügbar. Führe zuerst eine Migration aus.");
            CurrentStepText = "Kein Report verfügbar. Bitte zuerst Migration ausführen.";
            MessageBox.Show(
                "Kein Report verfügbar. Bitte zuerst eine Migration ausführen.",
                "Bericht exportieren",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var exportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "HyperBridge-Exports");
        Directory.CreateDirectory(exportDirectory);

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Bericht exportieren",
            Filter = "HTML-Bericht (*.html)|*.html|Alle Dateien (*.*)|*.*",
            DefaultExt = ".html",
            AddExtension = true,
            InitialDirectory = exportDirectory,
            FileName = Path.GetFileName(_lastReportPath),
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.Copy(_lastReportPath, saveDialog.FileName, overwrite: true);

            var txtSource = Path.ChangeExtension(_lastReportPath, ".txt");
            if (File.Exists(txtSource))
            {
                var txtTarget = Path.ChangeExtension(saveDialog.FileName, ".txt");
                File.Copy(txtSource, txtTarget, overwrite: true);
            }

            CurrentStepText = $"Bericht exportiert: {saveDialog.FileName}";
            _loggingService.LogInfo($"Report exportiert: {saveDialog.FileName}");

            MessageBox.Show(
                "Der Bericht wurde erfolgreich exportiert.",
                "Bericht exportieren",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Export fehlgeschlagen: {ex.Message}");
            CurrentStepText = "Bericht-Export fehlgeschlagen";
            MessageBox.Show(
                $"Der Bericht konnte nicht exportiert werden:{Environment.NewLine}{ex.Message}",
                "Bericht exportieren",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        await Task.CompletedTask;
    }

    private bool CanExportReport()
    {
        return !string.IsNullOrWhiteSpace(_lastReportPath) && File.Exists(_lastReportPath);
    }

    private void CopyLogsToClipboard()
    {
        var text = string.Join(Environment.NewLine, LiveLogEntries);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        System.Windows.Clipboard.SetText(text);
    }

    private void CopyTechnicalDetailsToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(TechnicalDetails))
        {
            System.Windows.Clipboard.SetText(TechnicalDetails);
        }
    }

    private void ToggleTheme()
    {
        _settings.Theme = string.Equals(_settings.Theme, "Dark", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";

        ThemeRequested?.Invoke(this, _settings.Theme);
        _ = SaveSettingsAsync();
    }

    private void ApplyVmFilter()
    {
        var previouslySelectedName = SelectedVm?.Name;

        FilteredVms.Clear();
        var filtered = AvailableVms.Where(vm =>
            string.IsNullOrWhiteSpace(VmFilter)
            || vm.Name.Contains(VmFilter, StringComparison.OrdinalIgnoreCase)
            || vm.GuestOsType.Contains(VmFilter, StringComparison.OrdinalIgnoreCase));

        foreach (var vm in filtered)
        {
            FilteredVms.Add(vm);
        }

        if (!string.IsNullOrWhiteSpace(previouslySelectedName))
        {
            SelectedVm = FilteredVms.FirstOrDefault(vm => string.Equals(vm.Name, previouslySelectedName, StringComparison.OrdinalIgnoreCase));
        }

        // If there is exactly one VM, select it automatically to make the next action obvious.
        if (SelectedVm is null && FilteredVms.Count == 1)
        {
            SelectedVm = FilteredVms[0];
        }

        NextStepCommand.RaiseCanExecuteChanged();
    }

    private void ApplyVmResourceDefaults(VirtualMachineSummary summary)
    {
        if (summary.CpuCount > 0)
        {
            CpuCount = summary.CpuCount;
        }

        if (summary.MemoryMb <= 0)
        {
            return;
        }

        var normalizedMemoryMb = NormalizeMbToWholeGb(summary.MemoryMb);
        MemoryMb = normalizedMemoryMb;
        StartupMemoryMb = normalizedMemoryMb;
        MinimumMemoryMb = Math.Max(MbPerGb, normalizedMemoryMb / 2);
        MaximumMemoryMb = Math.Max(normalizedMemoryMb, normalizedMemoryMb * 2);
    }

    private static int MbFromGb(int gb)
    {
        var normalizedGb = Math.Max(1, gb);
        return normalizedGb * MbPerGb;
    }

    private static int GbFromMb(int mb)
    {
        if (mb <= 0)
        {
            return 1;
        }

        return Math.Max(1, mb / MbPerGb);
    }

    private static int NormalizeMbToWholeGb(int mb)
    {
        return MbFromGb(GbFromMb(mb));
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> action)
    {
        try
        {
            IsBusy = true;
            await action(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex.Message);
            TechnicalDetails = ex.ToString();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private MigrationRequest BuildMigrationRequest(VirtualMachineAnalysis analysis)
    {
        var assessment = new CompatibilityAssessment
        {
            Level = CompatibilityLevel,
            Recommendation = CompatibilitySummary,
            SuggestedGeneration = Generation,
        };
        assessment.Reasons.AddRange(CompatibilityReasons.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        assessment.Actions.AddRange(CompatibilityActions.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        return new MigrationRequest
        {
            Analysis = analysis,
            Target = BuildTargetConfiguration(),
            GuestChecklist = new GuestPreparationChecklist
            {
                HostnameChanged = HostnameChanged,
                LocalUsersReviewed = LocalUsersReviewed,
                UserPasswordsChanged = UserPasswordsChanged,
                AdminPasswordChanged = AdminPasswordChanged,
                NetworkParametersDocumented = NetworkParamsDocumented,
                GuestAdditionsRemovedOrPlanned = GuestAdditionsHandled,
                GuestShutdownCleanly = GuestShutdownCleanly,
                Notes = Notes,
            },
            Assessment = assessment,
        };
    }

    private TargetConfiguration BuildTargetConfiguration()
    {
        return new TargetConfiguration
        {
            HyperVVmName = string.IsNullOrWhiteSpace(HyperVVmName) ? (SelectedVm?.Name + "-HV") : HyperVVmName,
            TargetPath = TargetPath,
            Generation = Generation,
            MemoryMb = MemoryMb,
            DynamicMemoryEnabled = DynamicMemoryEnabled,
            StartupMemoryMb = StartupMemoryMb,
            MinimumMemoryMb = MinimumMemoryMb,
            MaximumMemoryMb = MaximumMemoryMb,
            CpuCount = CpuCount,
            VirtualSwitch = SelectedSwitch,
            SecureBootEnabled = SecureBootEnabled,
            StartAfterCreation = StartAfterCreation,
            CreateInitialCheckpoint = CreateCheckpoint,
            DryRun = DryRun,
        };
    }

    private bool CanStartMigration()
    {
        return !IsBusy
            && CanStartMigrationProcess
            && Analysis is not null
            && !string.IsNullOrWhiteSpace(TargetPath)
            && !string.IsNullOrWhiteSpace(HyperVVmName);
    }

    private string BuildPrerequisitesStatusMessage()
    {
        var issues = new List<string>();

        if (!IsAdmin)
        {
            issues.Add("Bitte HyperBridge als Administrator starten.");
        }

        if (!IsVirtualBoxDetected || !IsVBoxManageDetected)
        {
            issues.Add("VirtualBox/VBoxManage nicht erkannt.");
        }

        if (!IsHyperVDetected || !IsHyperVPowerShellDetected)
        {
            issues.Add("Hyper-V oder Hyper-V PowerShell nicht verfügbar.");
        }

        return issues.Count == 0
            ? "Bereit für Migration"
            : string.Join(" ", issues);
    }

    private static Version GetCurrentAppVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version
            ?? Assembly.GetExecutingAssembly().GetName().Version
            ?? new Version(0, 0, 0);

        return version;
    }

    private static string FormatVersionForDisplay(Version version)
    {
        var patch = version.Build >= 0 ? version.Build : 0;
        return $"{version.Major}.{version.Minor}.{patch}";
    }

    private static bool TryParseVersion(string rawValue, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        var versionToken = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? normalized;
        if (Version.TryParse(versionToken, out var parsedVersion) && parsedVersion is not null)
        {
            version = parsedVersion;
            return true;
        }

        return false;
    }

    private static HttpClient CreateUpdateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("HyperBridge-UpdateCheck");
        return client;
    }

    private void OnLogAdded(object? sender, LogEntry entry)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            LiveLogEntries.Add($"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}");
            while (LiveLogEntries.Count > 1000)
            {
                LiveLogEntries.RemoveAt(0);
            }
        });
    }

    private void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (File.Exists(path) || Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
    }

    private void OpenHyperVManager()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "virtmgmt.msc",
            UseShellExecute = true,
        });
    }

    private async Task SaveSettingsAsync()
    {
        _settings.LastDryRun = DryRun;
        _settings.LastSelectedVmName = SelectedVm?.Name ?? string.Empty;
        _settings.LastTargetPath = TargetPath;
        _settings.LastVirtualSwitch = SelectedSwitch;
        await _settingsService.SaveAsync(_settings, CancellationToken.None).ConfigureAwait(false);
    }
}


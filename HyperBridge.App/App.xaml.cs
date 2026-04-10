using System.Windows;
using System.IO;
using System.Threading;
using HyperBridge.App.ViewModels;
using HyperBridge.Core.Contracts;
using HyperBridge.Infrastructure;
using HyperBridge.Services;

namespace HyperBridge.App;

public partial class App : System.Windows.Application
{
	private static Mutex? _singleInstanceMutex;

	protected override void OnStartup(StartupEventArgs e)
	{
		var isOwned = false;
		_singleInstanceMutex = new Mutex(initiallyOwned: true, name: "Local\\HyperBridgeVmMigrator.SingleInstance", createdNew: out isOwned);
		if (!isOwned)
		{
			MessageBox.Show(
				"HyperBridge läuft bereits. Bitte nutze die bereits geöffnete Instanz.",
				"HyperBridge VM Migrator",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
			Shutdown();
			return;
		}

		base.OnStartup(e);

		DispatcherUnhandledException += (_, args) =>
		{
			WriteCrashLog("DispatcherUnhandledException", args.Exception);
			MessageBox.Show(
				"HyperBridge ist unerwartet auf einen Fehler gestoßen. Details wurden in eine Crash-Logdatei geschrieben.",
				"HyperBridge Fehler",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			args.Handled = true;
		};

		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			if (args.ExceptionObject is Exception ex)
			{
				WriteCrashLog("AppDomainUnhandledException", ex);
			}
		};

		TaskScheduler.UnobservedTaskException += (_, args) =>
		{
			WriteCrashLog("UnobservedTaskException", args.Exception);
			args.SetObserved();
		};

		try
		{
			var processRunner = new ProcessRunner();
			ILoggingService loggingService = new LoggingService();
			var powerShellRunner = new PowerShellRunner(processRunner);

			IVirtualBoxService virtualBoxService = new VirtualBoxService(processRunner, loggingService);
			IHyperVService hyperVService = new HyperVService(powerShellRunner, loggingService);
			ISystemCheckService systemCheckService = new SystemCheckService(virtualBoxService, hyperVService);
			IMigrationService migrationService = new MigrationService(virtualBoxService, hyperVService, systemCheckService, loggingService);
			IReportService reportService = new ReportService();
			ISettingsService settingsService = new SettingsService();

			var vm = new MainViewModel(
				virtualBoxService,
				hyperVService,
				migrationService,
				systemCheckService,
				loggingService,
				reportService,
				settingsService);

			var window = new MainWindow
			{
				DataContext = vm,
			};

			MainWindow = window;
			window.Show();

			_ = InitializeViewModelAsync(vm);
		}
		catch (Exception ex)
		{
			WriteCrashLog("Startup", ex);
			MessageBox.Show(
				"HyperBridge konnte nicht gestartet werden. Details wurden in eine Crash-Logdatei geschrieben.",
				"HyperBridge Fehler",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			Shutdown(-1);
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		try
		{
			_singleInstanceMutex?.ReleaseMutex();
			_singleInstanceMutex?.Dispose();
		}
		catch
		{
			// Ignore shutdown mutex exceptions.
		}

		base.OnExit(e);
	}

	private async Task InitializeViewModelAsync(MainViewModel vm)
	{
		try
		{
			await vm.InitializeAsync();
		}
		catch (Exception ex)
		{
			WriteCrashLog("InitializeViewModelAsync", ex);
			MessageBox.Show(
				"Beim Initialisieren von HyperBridge ist ein Fehler aufgetreten. Die Anwendung bleibt geöffnet, damit Logs exportiert werden können.",
				"HyperBridge Fehler",
				MessageBoxButton.OK,
				MessageBoxImage.Warning);
		}
	}

	private static void WriteCrashLog(string source, Exception ex)
	{
		try
		{
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var logDir = Path.Combine(appData, "HyperBridge", "Logs");
			Directory.CreateDirectory(logDir);
			var file = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
			File.WriteAllText(file, $"[{DateTime.Now:O}] {source}{Environment.NewLine}{ex}");
		}
		catch
		{
			// Ignore secondary logging failures.
		}
	}

}



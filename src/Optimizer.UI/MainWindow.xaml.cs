using System;
using System.Windows;
using Optimizer.Services;

namespace Optimizer.UI
{
    public partial class MainWindow : Window
    {
        private TempFilesCleaner _tempCleaner;
        private WindowsServiceManager _serviceManager;
        private SystemInfoService _systemInfoService;

        public MainWindow()
        {
            InitializeComponent();
            InitializeServices();
            LoadSystemInfo();
        }

        private void InitializeServices()
        {
            _tempCleaner = new TempFilesCleaner();
            _serviceManager = new WindowsServiceManager();
            _systemInfoService = new SystemInfoService();

            // Suscribirse a eventos
            _tempCleaner.OnProgressUpdated += TempCleaner_OnProgressUpdated;
            _tempCleaner.OnFilesDeleted += TempCleaner_OnFilesDeleted;

            _serviceManager.OnProgressUpdated += ServiceManager_OnProgressUpdated;
            _serviceManager.OnOperationCompleted += ServiceManager_OnOperationCompleted;
        }

        private void LoadSystemInfo()
        {
            try
            {
                var info = _systemInfoService.GetSystemInfo();
                CPUInfo.Text = info.ProcessorName;
                RAMInfo.Text = info.InstalledRAM;
                DiskInfo.Text = $"Libre: {info.AvailableDiskSpace} / Total: {info.TotalDiskSpace}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar información del sistema: {ex.Message}");
            }
        }

        /// <summary>
        /// Botón: Optimizar Todo
        /// </summary>
        private async void BtnOptimizeAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "⚠️ Esto ejecutará todas las optimizaciones disponibles.\n¿Deseas continuar?",
                "Confirmar Optimización",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            BtnOptimizeAll.IsEnabled = false;
            ProgressBar.Value = 0;
            StatusText.Text = "Estado: Optimizando...";
            ResultText.Text = "";

            try
            {
                // Ejecutar en segundo plano
                await System.Threading.Tasks.Task.Run(() =>
                {
                    // 1. Deshabilitar telemetría
                    Dispatcher.Invoke(() => StatusText.Text = "Estado: Deshabilitando telemetría...");
                    _serviceManager.DisableTelemetry();
                    Dispatcher.Invoke(() => ProgressBar.Value += 25);

                    // 2. Deshabilitar servicios
                    Dispatcher.Invoke(() => StatusText.Text = "Estado: Deshabilitando servicios...");
                    _serviceManager.DisableUnnecessaryServices();
                    Dispatcher.Invoke(() => ProgressBar.Value += 25);

                    // 3. Activar modo de rendimiento
                    Dispatcher.Invoke(() => StatusText.Text = "Estado: Activando modo de rendimiento...");
                    _serviceManager.EnablePerformanceMode();
                    Dispatcher.Invoke(() => ProgressBar.Value += 25);

                    // 4. Limpiar archivos temporales
                    Dispatcher.Invoke(() => StatusText.Text = "Estado: Limpiando archivos temporales...");
                    int filesDeleted = _tempCleaner.CleanAllTempFiles();
                    Dispatcher.Invoke(() => ProgressBar.Value = 100);
                    Dispatcher.Invoke(() => ResultText.Text = $"✅ Optimización completada. Archivos eliminados: {filesDeleted}");
                });
            }
            catch (Exception ex)
            {
                ResultText.Text = $"❌ Error: {ex.Message}";
            }
            finally
            {
                BtnOptimizeAll.IsEnabled = true;
                StatusText.Text = "Estado: Listo";
            }
        }

        /// <summary>
        /// Botón: Limpiar Archivos Temporales
        /// </summary>
        private async void BtnCleanTemp_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "¿Deseas limpiar los archivos temporales?",
                "Confirmar Limpieza",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            BtnCleanTemp.IsEnabled = false;
            ProgressBar.Value = 0;
            StatusText.Text = "Estado: Limpiando...";
            ResultText.Text = "";

            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    int filesDeleted = _tempCleaner.CleanAllTempFiles();
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = 100;
                        ResultText.Text = $"✅ Limpieza completada. {filesDeleted} archivos eliminados.";
                        StatusText.Text = "Estado: Listo";
                    });
                });
            }
            catch (Exception ex)
            {
                ResultText.Text = $"❌ Error: {ex.Message}";
                StatusText.Text = "Estado: Error";
            }
            finally
            {
                BtnCleanTemp.IsEnabled = true;
            }
        }

        /// <summary>
        /// Botón: Escanear Archivos Temporales
        /// </summary>
        private void BtnScanTemp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                long totalSize = _tempCleaner.GetTotalTempSize();
                double sizeInMB = totalSize / (1024.0 * 1024.0);
                TempSizeInfo.Text = $"Total: {sizeInMB:F2} MB";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al escanear: {ex.Message}");
            }
        }

        /// <summary>
        /// Botón: Limpiar Ahora (desde tab de Limpieza)
        /// </summary>
        private async void BtnCleanNow_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "¿Deseas eliminar todos los archivos temporales seleccionados?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            BtnCleanNow.IsEnabled = false;
            CleanupProgress.Value = 0;
            CleanupStatus.Text = "Limpiando...";

            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    int totalDeleted = 0;

                    if ((bool)ChkWindowsTemp.IsChecked)
                        totalDeleted += _tempCleaner.CleanWindowsTemp();

                    if ((bool)ChkWindowsTmp.IsChecked)
                        totalDeleted += _tempCleaner.CleanWindowsTmp();

                    // Agregar más opciones según sea necesario...

                    Dispatcher.Invoke(() =>
                    {
                        CleanupProgress.Value = 100;
                        CleanupStatus.Text = $"✅ Completado. {totalDeleted} archivos eliminados.";
                    });
                });
            }
            catch (Exception ex)
            {
                CleanupStatus.Text = $"❌ Error: {ex.Message}";
            }
            finally
            {
                BtnCleanNow.IsEnabled = true;
            }
        }

        /// <summary>
        /// Botón: Aplicar Cambios en Servicios
        /// </summary>
        private async void BtnApplyServices_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "⚠️ Esto modificará servicios de Windows.\n¿Deseas continuar?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            BtnApplyServices.IsEnabled = false;
            ServicesStatus.Text = "Procesando...";

            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    _serviceManager.DisableUnnecessaryServices();
                    if ((bool)ChkPerformance.IsChecked)
                        _serviceManager.EnablePerformanceMode();

                    Dispatcher.Invoke(() => ServicesStatus.Text = "✅ Cambios aplicados correctamente.");
                });
            }
            catch (Exception ex)
            {
                ServicesStatus.Text = $"❌ Error: {ex.Message}";
            }
            finally
            {
                BtnApplyServices.IsEnabled = true;
            }
        }

        // Event Handlers
        private void TempCleaner_OnProgressUpdated(object sender, string message)
        {
            Dispatcher.Invoke(() => StatusText.Text = $"Estado: {message}");
        }

        private void TempCleaner_OnFilesDeleted(object sender, int count)
        {
            Dispatcher.Invoke(() => ResultText.Text = $"✅ {count} archivos eliminados");
        }

        private void ServiceManager_OnProgressUpdated(object sender, string message)
        {
            Dispatcher.Invoke(() => StatusText.Text = $"Estado: {message}");
        }

        private void ServiceManager_OnOperationCompleted(object sender, bool success)
        {
            Dispatcher.Invoke(() =>
            {
                ResultText.Text = success ? "✅ Operación completada" : "❌ Operación fallida";
            });
        }
    }
}

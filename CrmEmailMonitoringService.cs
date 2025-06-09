using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using log4net;

namespace ASC.Mail.Enhanced
{
    /// <summary>
    /// Windows Service / SystemD service wrapper for CRM Email Monitoring Job
    /// This can run as a standalone service or be embedded in the main application
    /// </summary>
    public class CrmEmailMonitoringService : BackgroundService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CrmEmailMonitoringService));
        private readonly ILogger<CrmEmailMonitoringService> _logger;
        
        public CrmEmailMonitoringService(ILogger<CrmEmailMonitoringService> logger)
        {
            _logger = logger;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Info("🚀 CrmEmailMonitoringService: Starting CRM Email Monitoring Service...");
            _logger?.LogInformation("🚀 CrmEmailMonitoringService: Starting CRM Email Monitoring Service...");
            
            try
            {
                // Wait a bit for the application to fully initialize
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                
                // Start the monitoring job
                CrmEmailMonitoringJob.StartMonitoring();
                
                Log.Info("✅ CrmEmailMonitoringService: CRM Email Monitoring Service started successfully");
                _logger?.LogInformation("✅ CrmEmailMonitoringService: CRM Email Monitoring Service started successfully");
                
                // Keep the service running
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    
                    // Optional: Add periodic health checks here
                    Log.Debug("💓 CrmEmailMonitoringService: Service is running...");
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when cancellation is requested
                Log.Info("🛑 CrmEmailMonitoringService: Service cancellation requested");
            }
            catch (Exception ex)
            {
                Log.Error("❌ CrmEmailMonitoringService: Critical error in service execution", ex);
                _logger?.LogError(ex, "❌ CrmEmailMonitoringService: Critical error in service execution");
                throw;
            }
        }
        
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            Log.Info("🛑 CrmEmailMonitoringService: Stopping CRM Email Monitoring Service...");
            _logger?.LogInformation("🛑 CrmEmailMonitoringService: Stopping CRM Email Monitoring Service...");
            
            try
            {
                CrmEmailMonitoringJob.StopMonitoring();
                Log.Info("✅ CrmEmailMonitoringService: CRM Email Monitoring Service stopped successfully");
                _logger?.LogInformation("✅ CrmEmailMonitoringService: CRM Email Monitoring Service stopped successfully");
            }
            catch (Exception ex)
            {
                Log.Error("❌ CrmEmailMonitoringService: Error stopping service", ex);
                _logger?.LogError(ex, "❌ CrmEmailMonitoringService: Error stopping service");
            }
            
            await base.StopAsync(stoppingToken);
        }
    }
    
    /// <summary>
    /// Static initializer for applications that don't use dependency injection
    /// </summary>
    public static class CrmEmailMonitoringInitializer
    {
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();
        
        /// <summary>
        /// Initialize the CRM email monitoring for applications without DI
        /// Call this from Global.asax Application_Start or similar
        /// </summary>
        public static void Initialize()
        {
            lock (_lockObject)
            {
                if (_isInitialized) return;
                
                try
                {
                    Log.Info("🚀 CrmEmailMonitoringInitializer: Initializing CRM Email Monitoring...");
                    
                    // Start monitoring after a short delay
                    Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        CrmEmailMonitoringJob.StartMonitoring();
                    });
                    
                    _isInitialized = true;
                    Log.Info("✅ CrmEmailMonitoringInitializer: CRM Email Monitoring initialized successfully");
                }
                catch (Exception ex)
                {
                    Log.Error("❌ CrmEmailMonitoringInitializer: Error initializing CRM Email Monitoring", ex);
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Shutdown the monitoring (call from Application_End or similar)
        /// </summary>
        public static void Shutdown()
        {
            lock (_lockObject)
            {
                if (!_isInitialized) return;
                
                try
                {
                    Log.Info("🛑 CrmEmailMonitoringInitializer: Shutting down CRM Email Monitoring...");
                    CrmEmailMonitoringJob.StopMonitoring();
                    _isInitialized = false;
                    Log.Info("✅ CrmEmailMonitoringInitializer: CRM Email Monitoring shutdown complete");
                }
                catch (Exception ex)
                {
                    Log.Error("❌ CrmEmailMonitoringInitializer: Error shutting down CRM Email Monitoring", ex);
                }
            }
        }
    }
}
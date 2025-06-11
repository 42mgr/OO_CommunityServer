/*
 * Web CRM Monitoring Service
 * Simplified background service that uses existing CrmEmailAutoLinkService
 * Monitors for new emails and triggers enhanced CRM linking
 */

using System;
using System.Threading;
using ASC.Common.Logging;

namespace ASC.Mail.Core.Engine
{
    public static class WebCrmMonitoringService
    {
        private static readonly ILog Log = LogManager.GetLogger("ASC.Mail.WebCrmMonitoringService");
        private static Timer _monitoringTimer;
        private static readonly object _lockObject = new object();
        private static bool _isRunning = false;
        
        public static bool IsRunning
        {
            get { return _isRunning; }
        }
        
        public static void Start()
        {
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    Log.Info("WebCrmMonitoringService is already running");
                    return;
                }
                
                Log.Info("Starting WebCrmMonitoringService...");
                
                // Start the CRM auto-linking service once
                CrmEmailAutoLinkService.Start();
                
                // Set up status monitoring (no repeated triggering needed)
                _monitoringTimer = new Timer(MonitorStatus, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
                _isRunning = true;
                
                Log.Info("WebCrmMonitoringService started successfully");
            }
        }
        
        public static void Stop()
        {
            lock (_lockObject)
            {
                if (!_isRunning)
                {
                    Log.Info("WebCrmMonitoringService is not running");
                    return;
                }
                
                Log.Info("Stopping WebCrmMonitoringService...");
                
                if (_monitoringTimer != null)
                {
                    _monitoringTimer.Dispose();
                    _monitoringTimer = null;
                }
                
                // Stop the underlying CRM service too
                CrmEmailAutoLinkService.Stop();
                
                _isRunning = false;
                
                Log.Info("WebCrmMonitoringService stopped successfully");
            }
        }
        
        public static string GetStatus()
        {
            return _isRunning ? "Running" : "Stopped";
        }
        
        private static void MonitorStatus(object state)
        {
            try
            {
                lock (_lockObject)
                {
                    if (!_isRunning) return;
                    
                    Log.DebugFormat("WebCrmMonitoringService: Monitoring CRM auto-linking service status");
                    
                    // Just log status - the service runs independently now
                    Log.DebugFormat("WebCrmMonitoringService: Service is running and monitoring emails");
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("WebCrmMonitoringService: Error in MonitorStatus: {0}", ex.Message);
            }
        }
    }
}
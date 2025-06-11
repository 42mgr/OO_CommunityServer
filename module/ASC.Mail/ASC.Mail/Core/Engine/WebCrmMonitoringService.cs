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
                
                // Start timer to trigger CRM auto-linking every 60 seconds
                _monitoringTimer = new Timer(TriggerCrmAutoLinking, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
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
                
                _isRunning = false;
                
                Log.Info("WebCrmMonitoringService stopped successfully");
            }
        }
        
        public static string GetStatus()
        {
            return _isRunning ? "Running" : "Stopped";
        }
        
        private static void TriggerCrmAutoLinking(object state)
        {
            try
            {
                lock (_lockObject)
                {
                    if (!_isRunning) return;
                    
                    Log.DebugFormat("WebCrmMonitoringService: Triggering CRM auto-linking process");
                    
                    // Use the existing CrmEmailAutoLinkService to process emails
                    // This service already has all the database access and logic
                    try
                    {
                        CrmEmailAutoLinkService.Start();
                        Log.DebugFormat("WebCrmMonitoringService: CRM auto-linking process triggered successfully");
                    }
                    catch (Exception ex)
                    {
                        Log.WarnFormat("WebCrmMonitoringService: Failed to start CrmEmailAutoLinkService: {0}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("WebCrmMonitoringService: Error in TriggerCrmAutoLinking: {0}", ex.Message);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Config;

namespace BlaiseCaseHandler
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        
        // Instantiate logger.
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void Main()
        {
            // Call the service class if run in debug mode so no need to install service for testing.
#if DEBUG
            log.Info("Blaise Case Handler service starting in DEBUG mode.");
            BlaiseCaseHandler bchService = new BlaiseCaseHandler();
            bchService.OnDebug();
#else
            log.Info("Blaise Case Handler service starting in RELEASE mode.");
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new BlaiseCaseHandler()
            };
            ServiceBase.Run(ServicesToRun);
#endif
        }
    }
}

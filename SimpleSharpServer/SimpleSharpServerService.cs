using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using MySharpServer.Common;
using MySharpServer.Framework;

namespace SimpleSharpServer
{
    partial class SimpleSharpServerService : ServiceBase
    {
        CommonServerContainerSetting m_ServerSetting = null;
        CommonServerContainer m_Server = null;

        public SimpleSharpServerService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // TODO: Add code here to start your service.

            RequestAdditionalTime(1000 * Program.EXTRA_START_SVC_SECONDS);

            RemoteCaller.HttpConnectionLimit = 1000; // by default

            CommonLog.Info("=== " + Program.SVC_NAME + " is starting ===");

            ConfigurationManager.RefreshSection("appSettings");

            var appSettings = ConfigurationManager.AppSettings;

            var allKeys = appSettings.AllKeys;

            foreach (var key in allKeys)
            {
                if (key == "OutgoingHttpConnectionLimit")
                    RemoteCaller.HttpConnectionLimit = Convert.ToInt32(appSettings[key].ToString());

                if (key == "DefaultRemoteCallTimeout")
                    RemoteCaller.DefaultTimeout = Convert.ToInt32(appSettings[key].ToString());

                if (key == "AppServerSetting")
                    m_ServerSetting = JsonConvert.DeserializeObject<CommonServerContainerSetting>(appSettings[key]);
            }

            if (m_Server == null && m_ServerSetting != null) m_Server = new CommonServerContainer();

            if (m_Server != null && !m_Server.IsWorking() && m_ServerSetting != null)
            {
                m_Server.Start(m_ServerSetting, CommonLog.GetLogger());
                Thread.Sleep(100);
            }
        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.

            RequestAdditionalTime(1000 * Program.EXTRA_STOP_SVC_SECONDS);

            if (m_Server != null && m_Server.IsWorking())
            {
                m_Server.Stop();
                Thread.Sleep(100);
            }

            CommonLog.Info("=== " + Program.SVC_NAME + " stopped ===");
        }
    }
}

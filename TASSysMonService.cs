using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using System.Net.Mail;

namespace TASSysMon
{

    public partial class TASSysMonService : ServiceBase
    {
        private int eventId = 1;
        public TASSysMonService()
        {
            InitializeComponent();
            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("TASSysMonitorService"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "TASSysMonitorService", "TASSysMonLog");
            }
            eventLog1.Source = "TASSysMonitorService";
            eventLog1.Log = "TASSysMonLog";
        }

        protected override void OnStart(string[] args)
        {
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            eventLog1.WriteEntry("Started... Deploy 1st April 2020");
            Timer timer = new Timer();
            timer.Interval = 60000;
            timer.Elapsed += new ElapsedEventHandler(this.onTimer);
            timer.Start();

            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

        }

        private void onTimer(object sender, ElapsedEventArgs e)
        {
            eventLog1.WriteEntry("Monitoring...", EventLogEntryType.Information, eventId++);

            TimeSpan checkTimeStart = new TimeSpan(07, 00, 30);
            TimeSpan checkTimeEnd = new TimeSpan(07, 01, 30);
            TimeSpan timeNow = DateTime.Now.TimeOfDay;

            if ((timeNow >= checkTimeStart) && (timeNow <= checkTimeEnd))
            {
                try
                {
                    using (var db = new SQL02Thas01Entities())
                    {
                        using (var cDb = new SQL02ConnectDbEntities())
                        {
                            cDb.Database.CommandTimeout = 40000;
                            db.Database.CommandTimeout = 40000;
                            var todayDate = DateTime.Now.Date;
                            var checkUID = cDb.MRPMonitorEntries.Count() > 0 ? cDb.MRPMonitorEntries.Select(x => x.UID).Max() : 0;
                            var checkDate = cDb.MRPMonitorEntries.Count() > 0 ? cDb.MRPMonitorEntries.Select(x => x.TimeChecked).Max().Date : DateTime.MinValue.Date;
                            var setUID = ++checkUID;

                            if (checkDate != todayDate)
                            {
                                var latestMRPRecords = db.THAS_CONNECT_MRPChecker().ToList();

                                if (latestMRPRecords.Count() > 0)
                                {
                                    var mrpRecord = latestMRPRecords.OrderByDescending(x => x.MRPID).First();
                                    var MRPMonitorEntry = new MRPMonitorEntry
                                    {
                                        MRPID = mrpRecord.MRPID,
                                        Duration = mrpRecord.Duration.HasValue ? mrpRecord.Duration.Value : 0,
                                        CancelPOSuggs = mrpRecord.Cancel_PO_Suggs.HasValue ? mrpRecord.Cancel_PO_Suggs.Value : 0,
                                        CancelWOSuggs = mrpRecord.Cancel_WO_Suggs.HasValue ? mrpRecord.Cancel_WO_Suggs.Value : 0,
                                        StartMRPOn = mrpRecord.StartMRPOn,
                                        FinishMRPOn = mrpRecord.FinishMRPOn.HasValue ? mrpRecord.FinishMRPOn.Value : DateTime.MinValue,
                                        MRPStatus = mrpRecord.Result,
                                        NewPOSuggs = mrpRecord.New_PO_Suggs.HasValue ? mrpRecord.New_PO_Suggs.Value : 0,
                                        NewWOSuggs = mrpRecord.New_WO_Suggs.HasValue ? mrpRecord.New_WO_Suggs.Value : 0,
                                        ReschedulePOSuggs = mrpRecord.Reschedule_PO_Suggs.HasValue ? mrpRecord.Reschedule_PO_Suggs.Value : 0,
                                        RescheduleWOSuggs = mrpRecord.Reschedule_WO_Suggs.HasValue ? mrpRecord.Reschedule_WO_Suggs.Value : 0,
                                        TimeChecked = DateTime.Now,
                                        UID = setUID
                                    };

                                    cDb.MRPMonitorEntries.Add(MRPMonitorEntry);
                                    cDb.SaveChanges();
                                    SendMail("MRP Run Successful", "MRP job ran at <b>" 
                                        + mrpRecord.StartMRPOn + "</b> and finished at <b>" + MRPMonitorEntry.FinishMRPOn 
                                        + "</b> with a total duration of <b>" + mrpRecord.Duration + "</b> minutes." 
                                        + "</br></br> At the time of this email, there are: </br></br>"
                                        + "PO New Suggestions: <b>" + MRPMonitorEntry.NewPOSuggs + "</b>. </br>WO New Suggestions: <b>" + MRPMonitorEntry.NewWOSuggs 
                                        + "</b>. </br>PO Reschedules: <b>" + MRPMonitorEntry.ReschedulePOSuggs + "</b>. </br>WO Reschedules <b>" + MRPMonitorEntry.RescheduleWOSuggs
                                        + "</b>. </br>PO Cancellations: <b>" + MRPMonitorEntry.CancelPOSuggs + "</b>. </br>WO Cancellations: <b>" + MRPMonitorEntry.CancelWOSuggs
                                        + "</b>. </br></br>Dont Forget To Wash Your Hands."
                                        + "</br></br>Take Care & God Bless.");
                                }
                                else
                                {
                                    var MRPMonitorEntry = new MRPMonitorEntry
                                    {
                                        MRPID = 0,
                                        Duration = 0,
                                        CancelPOSuggs = 0,
                                        CancelWOSuggs = 0,
                                        StartMRPOn = DateTime.Now,
                                        FinishMRPOn = DateTime.Now,
                                        MRPStatus = "Failure",
                                        NewPOSuggs = 0,
                                        NewWOSuggs = 0,
                                        ReschedulePOSuggs = 0,
                                        RescheduleWOSuggs = 0,
                                        TimeChecked = DateTime.Now,
                                        UID = setUID
                                    };
                                    SendMail("MRP Run Failure", "No Record In MRP Log Table. Please investigate.");
                                }
                            }
                            else
                            {
                                // Already recorded an entry for today.
                                SendTestMail("Testing", "Just a Test");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    eventLog1.WriteEntry("MRP Check Failed. " + ex.Message + ex.InnerException, EventLogEntryType.Information, eventId++);
                    SendTechnicalMail("MRP Check Failure", "Something went wrong with the windows service. " + ex.Message + ex.InnerException);
                }
            }
            else
            {
                SendTestMail("Test", "Not In Window. TimeStart: " + checkTimeStart.ToString("hh:mm:ss") + ". TimeEnd: " +checkTimeEnd.ToString("hh:mm:ss"));
            }
        }

        protected override void OnStop()
        {
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            eventLog1.WriteEntry("In OnStop.");
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }
        protected override void OnContinue()
        {
            eventLog1.WriteEntry("In OnContinue.");
        }
        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        };

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

        private static void SendMail(string message, string result)
        {
            try
            {
                string from = "MRPJobMonitor@thompsonaero.com";
                string addresses = "felix.curran@thompsonaero.com;sean.kelly@thompsonaero.com; chris.weeks@thompsonaero.com; brendan.mcclenaghan@thompsonaero.com";
                //string to = "sean.kelly@thompsonaero.com";

                using (MailMessage mail = new MailMessage(from, addresses.Replace(";",",")))
                {
                    mail.Subject = message;
                    mail.Body = result;
                    mail.IsBodyHtml = true;
                    SmtpClient client = new SmtpClient("remote.thompsonaero.com");
                    client.Send(mail);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.InnerException);
            }
        }

        private static void SendTechnicalMail(string message, string result)
        {
            try
            {
                string from = "MRPJobTechMonitor@thompsonaero.com";
                string addresses = "sean.kelly@thompsonaero.com";
                //string to = "sean.kelly@thompsonaero.com";

                using (MailMessage mail = new MailMessage(from, addresses.Replace(";", ",")))
                {
                    mail.Subject = message;
                    mail.Body = result;
                    mail.IsBodyHtml = true;
                    SmtpClient client = new SmtpClient("remote.thompsonaero.com");
                    client.Send(mail);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.InnerException);
            }
        }

        private static void SendTestMail(string message, string result)
        {
            try
            {
                string from = "MRPJobTestMonitor@thompsonaero.com";
                string addresses = "sean.kelly@thompsonaero.com";

                using (MailMessage mail = new MailMessage(from, addresses.Replace(";", ",")))
                {
                    mail.Subject = message;
                    mail.Body = result;
                    mail.IsBodyHtml = true;
                    SmtpClient client = new SmtpClient("remote.thompsonaero.com");
                    client.Send(mail);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.InnerException);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using GSLibrary;
using tcpServer;

namespace SATService
{
    public class MainClass
    { //Rawr
        //Error codes

        //1: Proxy treated like server
        //2: Null fallback unhandled
        static string VersionNumber = "1.31";

        static List<PendingAction> Actions = new List<PendingAction>();
        static List<PendingString> ActionStrings = new List<PendingString>();
        static TcpServer TCP = new TcpServer();
        private static string key = "";
        static List<int> DefaultFallback = new List<int> { 0, 3, 1, 2 };
        static System.Timers.Timer T;

        static Process BackupProcess = new Process();
        static Boolean FirstBackupDone = false;
        static Task AutoBackupTask;
        static Boolean WebEnabled = false, AutoBackupEnabled = true, TCPEnabled = true;
        static Memory Mem;
        static Boolean WrittenToMem = false;
        static string Timestamp {  get { return DateTime.Now.ToString("F") + ": "; } }
        static Boolean Expertimental = false;
        static Boolean PerformanceDebug = false;
        public static void Main(string[] args)
        {
            Mem = (Memory)GSMemoryLoader.LoadMemory("Memory.xml", typeof(Memory));
            Expertimental = args.Length != 0 && (args[0] == "1" | args[0] == "2");
            PerformanceDebug = args.Length != 0 && args[0] == "2";

            #region Web
            if (WebEnabled)
            {
                try
                {
                    WebServer Web = new WebServer();
                    Web.AddPrefixManual("", 80);
                    Web.AddPrefixManual("/map", 80);
                    Web.AddPrefixManual("/stats", 80);
                    Web.RequestReceived += Web_RequestReceived;
                    Console.WriteLine(Timestamp + "Booted redirection server");
                }
                catch (Exception e)
                {
                    Console.WriteLine(Timestamp + "Failed to boot redirection server");
                    Console.WriteLine(Timestamp + e.Message);
                }
            }
            #endregion

            #region AutoBackup
            if (AutoBackupEnabled)
            {
                AutoBackupTask = new Task(AutoBackupMethod);
                AutoBackupTask.Start();
            }
            #endregion

            #region TCP Setup
            if (TCPEnabled)
            {
                try
                {
                    key = "{REDACTED}";
                    T = new System.Timers.Timer
                    {
                        Interval = 250
                    };
                    T.Elapsed += TickElapsed;
                    T.Start();
                    TCP.Port = 2010;
                    TCP.OnConnect += TCP_OnConnect;
                    TCP.OnDataAvailable += TCP_OnDataAvailable;
                    try
                    {
                        TCP.Open();
                    }
                    catch
                    {
                        Console.Clear();
                        Console.WriteLine(Timestamp + "Error on boot, contact an Admin to solve this problem.");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                    Console.ReadLine();
                }
                catch (SocketException e)
                {
                    Console.WriteLine(Timestamp + "Socket exception");
                }
                catch (Exception e)
                {
                    Console.WriteLine(Timestamp + e.Message + "\n" + e.TargetSite + "\n" + e.ToString());
                }
            }
            #endregion
        }

        #region Web Event
        static void Web_RequestReceived(System.Net.HttpListenerRequest Request, System.Net.HttpListenerResponse Response)
        {
            switch (Request.RawUrl)
            {
                case "/map":
                    Response.Redirect("http://localhost:82"); //Website is gone now, since the business shut down
                    break;

                case "/stats":
                    Response.Redirect("http://localhost:81");
                    break;

                default:
                    Response.Redirect("Facebook Page"); //Also gone :(
                    break;
            }
            Response.Close();
        }
        #endregion

        #region AutoBackup Event
        static void AutoBackupMethod()
        {
            Console.WriteLine(Timestamp + "Auto Backup worker started!");
            while (true)
            {

                int msTo3PM = (int)DateTime.Now.Date.AddHours(15).AddDays(DateTime.Now.Hour >= 15 ? 1 : 0).Subtract(DateTime.Now).TotalMilliseconds;
                int msTo3AM = (int)DateTime.Now.Date.AddHours(3).AddDays(DateTime.Now.Hour >= 3 ? 1 : 0).Subtract(DateTime.Now).TotalMilliseconds;
                if (msTo3PM < msTo3AM)
                {
                    Thread.Sleep(msTo3PM);
                }
                else
                {
                    Thread.Sleep(msTo3AM);
                }

                if (!IsProcessRunning(BackupProcess))
                {
                    new Task(() =>
                    {
                        Say("Auto backup has started! Hub, Sat and Grind will restart over the next hour with a 5 minute warning", Server.Grind);
                        Say("Auto backup has started! Hub, Sat and Grind will restart over the next hour with a 5 minute warning", Server.Sat);
                        Say("Auto backup has started! Hub, Sat and Grind will restart over the next hour with a 5 minute warning", Server.Hub);
                        GetArg();
                        if (File.Exists("/home/archive/database.sql"))
                        {
                            RunCommand("rm", "/home/archive/database.sql");
                        }
                        RunCommand("sh", "/root/mysqldump");
                        FirstBackupDone = true;
                        BackupProcess.Start();
                    }).Start();
                }
                Thread.Sleep(1200000);
                QueueRestart(Server.Sat, 1);
                Thread.Sleep(1200000);
                QueueRestart(Server.Hub, 1);
                Thread.Sleep(1200000);
                QueueRestart(Server.Grind, 1);
            }
        }

        static void QueueRestart(Server server, int Message)
        {
            int SelectedServer = (int)server;
            if (AllowedRestarts[SelectedServer] == 1 && PollScreens()[SelectedServer])
            {
                PendingAction A = new PendingAction();
                A.ServerToClose = server;
                if (PollScreens()[DefaultFallback[SelectedServer]])
                {
                    A.ServerToSendTo = (Server)DefaultFallback[SelectedServer];
                }
                else
                {
                    int Alt = new List<int> { 1, 2, 3 }.Except(new List<int> { SelectedServer, DefaultFallback[SelectedServer] }).ToList()[0];
                    if (PollScreens()[Alt])
                    {
                        A.ServerToSendTo = (Server)Alt;
                    }
                    else
                    {
                        A.ServerToSendTo = Server.Null;
                    }
                }
                A.Timer = 600;
                A.AlertInterval = 60;
                A.TenSecondCount = true;
                A.User = "root";
                A.Restart = 1;
                Announce(String.Format(RestartMessages[Message], ServerNames[SelectedServer]), String.Format("{0} - {1}", GetTime(A.Timer), FallbackMessages[(int)A.ServerToSendTo]), 10);
                Actions.Add(A);
            }
        }
        #endregion

        #region TCP
        static void TCP_OnConnect(TcpServerConnection connection)
        {
            try
            {
                string IP = connection.Socket.Client.RemoteEndPoint.ToString();
                string Message = "";
                IP = IP.Substring(0, IP.IndexOf(":"));
                if (IP != "127.0.0.1")
                {
                    connection.Socket.Close();
                    Message = " ...Kicked";
                }
                else
                {
                    connection.sendData(String.Format("Server|versiondata,{0}*", VersionNumber));
                    Message = " ...Version data sent";
                }
                Console.WriteLine(Timestamp + IP + Message);
            }
            catch
            {

            }
        }

        static void TCP_OnDataAvailable(TcpServerConnection connection)
        {
            try
            {
                string text = readStream(connection.Socket);
                #region Setup
                string Decrypted = text;
                string Original = "";
                string OriginalDecrypted = "";
                string Auth = "", Command = "";
                List<string> Args = new List<string>();
                try
                {
                    Decrypted = Decrypted.Replace(((char)19).ToString(), "");
                    Original = Decrypted;
                    //Decrypted = AuthCrypt.DecryptString(Decrypted, key); //Comment this to disable Encryption
                    OriginalDecrypted = Decrypted;
                }
                #region Catch
                catch
                {

                }
                #endregion

                //Auth Key
                if (Decrypted.Contains("|"))
                {
                    Auth = Decrypted.Split('|')[0];
                    Decrypted = Decrypted.Split('|')[1];
                }

                //Command
                if (Decrypted != "")
                {
                    if (Decrypted.Contains(","))
                    {
                        Command = Decrypted.Split(',')[0];
                        Decrypted = Decrypted.Substring(Decrypted.IndexOf(",") + 1);
                    }
                    else
                    {
                        Command = Decrypted;
                    }
                }

                //Args
                if (Decrypted != "")
                {
                    if (Decrypted.Contains(","))
                    {
                        foreach (string a in Decrypted.Split(','))
                        {
                            if (a != "")
                            {
                                Args.Add(a);
                            }
                        }
                    }
                    else
                    {
                        Args.Add(Decrypted);
                    }
                }
                #endregion
                List<int> ErrorCodes = new List<int>(); //0 = Success, 1 = Existing action, 2 = No fallback, 3 = Alt fallback used, 4 = Restart disabled, 5 = Not allowed, 6 = Backup already in progress
                switch (Command)
                {
                    case "stoprestart":
                        Actions.RemoveAll(x => x.User == Auth);
                        ActionStrings.Add(new PendingString { Send = "Server|ShowError,0" , WhoToSendTo = connection });
                        break;
                    case "restart": //Server = 1-3, Timer = int, TenSecondAlert = 0-1, Announced = 0-1, Fallback = 0-3, AlertInterval = int, Toggle = 0-1, Kill = 0-1

                        PendingAction A = new PendingAction();
                        int SelectedServer = Convert.ToInt32(Args[0]);
                        Mem.LoggedActions.Add(new LoggedAction() { dateTime = DateTime.Now, TargettedServer = SelectedServer, User = Auth } );
                        WrittenToMem = true;
                        if (AllowedRestarts[SelectedServer] == 0)
                        {
                            ErrorCodes.Add(4);
                            ActionStrings.Add(new PendingString { Send = "Server|ShowError," + String.Join(",", ErrorCodes), WhoToSendTo = connection });
                            break;
                        }
                        int SelectedFallback = Convert.ToInt32(Args[4]);
                        if (SelectedServer == 4)
                        {
                            A.ServerToClose = (Server)SelectedServer;
                            A.Timer = Convert.ToInt32(Args[1]);
                            A.User = Auth;
                            if (Args[2] == "1")
                            {
                                A.TenSecondCount = true;
                            }
                            A.ServerToSendTo = Server.Proxy;
                            A.Restart = Convert.ToInt32(Args[6]);
                            A.Kill = Convert.ToInt32(Args[7]);
                            if (Args[3] == "1")
                            {
                                Announce(String.Format("{0}-Reset!", ServerNames[SelectedServer]), String.Format("{0} - {1}", GetTime(A.Timer), FallbackMessages[(int)A.ServerToSendTo]), 10);
                            }
                            A.AlertInterval = Convert.ToInt32(Args[5]);
                            Actions.Add(A);
                            ErrorCodes.Add(0);
                            ActionStrings.Add(new PendingString { Send = "Server|ShowError," + String.Join(",", ErrorCodes), WhoToSendTo = connection });
                            Console.WriteLine(Timestamp + "Sent!");
                        }
                        else
                        {
                            int AltFallback = new List<int> { 1, 2, 3 }.Except(new List<int> { SelectedServer, DefaultFallback[SelectedServer] }).ToList()[0];
                            List<Boolean> AvailableFallbacks = PollScreens();
                            if (!AvailableFallbacks[SelectedServer])
                            {
                                StartServer((Server)SelectedServer);
                                ErrorCodes.Add(0);
                                ActionStrings.Add(new PendingString { Send = "Server|ShowError," + String.Join(",", ErrorCodes), WhoToSendTo = connection });
                                break;
                            }
                            if (Actions.Where(x => x.ServerToClose == (Server)SelectedServer).ToList().Count == 0)
                            {
                                A.ServerToClose = (Server)SelectedServer;
                                A.Timer = Convert.ToInt32(Args[1]);
                                A.User = Auth;
                                if (Args[2] == "1")
                                {
                                    A.TenSecondCount = true;
                                }
                                if (SelectedServer != 0)
                                {
                                    if (SelectedFallback != 0 && AvailableFallbacks[SelectedFallback] && SelectedServer != SelectedFallback)
                                    {
                                        A.ServerToSendTo = (Server)SelectedFallback;
                                    }
                                    else if (AvailableFallbacks[DefaultFallback[SelectedServer]])
                                    {
                                        A.ServerToSendTo = (Server)DefaultFallback[SelectedServer];
                                    }
                                    else if (AvailableFallbacks[AltFallback])
                                    {
                                        ErrorCodes.Add(3);
                                        A.ServerToSendTo = (Server)AltFallback;
                                    }
                                    else
                                    {
                                        ErrorCodes.Add(2);
                                        A.ServerToSendTo = Server.Null;
                                    }
                                }
                                else
                                {
                                    A.ServerToSendTo = Server.Null;
                                }
                                A.Restart = Convert.ToInt32(Args[6]);
                                A.Kill = Convert.ToInt32(Args[7]);
                                if (Args[3] == "1")
                                {
                                    Announce(String.Format("{0}-Reset!", ServerNames[SelectedServer]), String.Format("{0} - {1}", GetTime(A.Timer), FallbackMessages[(int)A.ServerToSendTo]), 10);
                                }
                                A.AlertInterval = Convert.ToInt32(Args[5]);
                                Actions.Add(A);
                                ErrorCodes.Add(0);
                                ActionStrings.Add(new PendingString { Send = "Server|ShowError," + String.Join(",", ErrorCodes), WhoToSendTo = connection });
                                Console.WriteLine(Timestamp + "Sent!");
                            }
                            else
                            {
                                ActionStrings.Add(new PendingString { Send = "Server|ShowError,1", WhoToSendTo = connection });
                            }
                        }
                        break;
                    case "killswitch":
                        AllowedRestarts = Args.Select(x => Convert.ToInt32(x)).ToList();
                        AllowedRestarts.Add(1);
                        ActionStrings.Add(new PendingString { Send = "Server|killswitch,0", WhoToSendTo = connection });
                        break;
                    case "backup":
                        if (IsProcessRunning(BackupProcess))
                        {
                            ErrorCodes.Add(6);
                            ActionStrings.Add(new PendingString { Send = "Server|ShowError," + String.Join(",", ErrorCodes), WhoToSendTo = connection });
                        }
                        else
                        {
                            GetArg();
                            if (File.Exists("/home/archive/database.sql"))
                            {
                                RunCommand("rm", "/home/archive/database.sql");
                            }
                            RunCommand("sh", "/root/mysqldump");
                            FirstBackupDone = true;
                            BackupProcess.Start();
                            ErrorCodes.Add(0);

                            ActionStrings.Add(new PendingString { Send = "Server|ShowError," + String.Join(",", ErrorCodes) , WhoToSendTo = connection } );
                        }
                        break;
                }
            }catch(Exception e1)
            {
                Console.WriteLine(Timestamp + e1.Message + "\n" + e1.TargetSite + "\n" + e1.ToString());
            }
        }

        static string readStream(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            string text = "";
            while (stream.DataAvailable)
            {
                int data = 0;
                data = stream.ReadByte();
                text += char.ConvertFromUtf32(data);

            }
            return text;
        }
        #endregion

        #region Ticks
        static int MsSinceLastTick = 0;
        static List<int> AllowedRestarts = new List<int>() { 1, 1, 1, 1, 1 };
        private static void TickElapsed(object sender, ElapsedEventArgs e)
        {
            Thread WorkerThread = new Thread(new ThreadStart(DoTick));
            WorkerThread.Start();
        }
        static List<string> LogCreation = new List<string>() { "", "", "", "" };
        static PerformanceCounter CPU = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        static void DoTick()
        {
            Stopwatch Watch = new Stopwatch();
            Watch.Start();
            Dictionary<string, long> WatchCounts = new Dictionary<string, long>();

            #region ActionString
            foreach(PendingString i in ActionStrings)
            {
                try
                {
                    i.WhoToSendTo.sendData(i.Send + "*");
                }
                catch 
                {

                }
            }
            ActionStrings.Clear();
            WatchCounts.Add("ActionStrings", Watch.ElapsedMilliseconds);
            #endregion

            #region TickData
            //Setting Starting and Disabled
            List<string> TickData = PollScreens().Select(x => x ? "1" : "0").ToList();
            for (int i = 0; i < 4; i++)
            {

                try
                {
                    if (AllowedRestarts[i] == 0)
                    {
                        TickData[i] = "4";
                    }
                    string Location = "/home/SuperAwesome/" + Folders[i] + "/server/logs/latest.log";
                    if (File.Exists(Location))
                    {
                        string Current = File.GetCreationTime("/home/SuperAwesome/" + Folders[i] + "/server/logs/latest.log").ToString("s");
                        if (LogCreation[i] != Current)
                        {
                            string Text = File.ReadAllText("/home/SuperAwesome/" + Folders[i] + "/server/logs/latest.log");
                            if (!(!Text.Contains("Done (") && Text.Contains("Starting")) && !(!Text.Contains("Listening on /0.0.0.0") && Text.Contains("Using mbed TLS based native")) && Text != "")
                            {
                                LogCreation[i] = Current;
                            }
                            else
                            {
                                TickData[i] = "3";
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(Timestamp + "Error at TickData code setting 3 and 4: \n" + e.Message);
                }
            }
            WatchCounts.Add("Setting starting and disabled", Watch.ElapsedMilliseconds);

            //Setting uptime of online servers
            if (Expertimental)
            {
                string[] psdata = { "", "", "", "" };
                long[] Uptimes = { 0, 0, 0, 0 };

                string[] psraw = RunCommand("ps", "-eo pid,etime,command").Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                List<string> Matches = new List<string>();
                for(int i = 0; i < 4; i++)
                {
                    if(TickData[i] == "1")
                    {
                        Matches.Add(ScreenNames[i]);
                    }
                }
                foreach (string a in psraw)
                {
                    for(int i = 0; i < 4; i++)
                    {
                        if (a.Contains("S " + ScreenNames[i]))
                        {
                            string Extracted = a.Substring(0, a.IndexOf(" /usr/bin")).Trim();
                            Extracted = Extracted.Substring(Extracted.IndexOf(" ")).Trim();
                            List<long> UptimeData = Extracted.Split(new string[] { "-", ":" }, StringSplitOptions.RemoveEmptyEntries).Select(x => Convert.ToInt64(x)).ToList();
                            while (UptimeData.Count < 4)
                            {
                                UptimeData.Insert(0, 0);
                            }
                            long Seconds = UptimeData[0] * 86400;
                            Seconds += UptimeData[1] * 3600;
                            Seconds += UptimeData[2] * 60;
                            Seconds += UptimeData[3];
                            TimeSpan t = TimeSpan.FromSeconds(Seconds);
                            string TickDataSet = "";
                            if (Seconds > 86399)
                            {
                                TickDataSet = string.Format("{0:00}d- {1:00}h", t.Days, t.Hours);
                            }
                            else if (Seconds > 3599)
                            {
                                TickDataSet = string.Format("{0:00}h- {1:00}m", t.Hours, t.Minutes);
                            }
                            else if (Seconds > 59)
                            {
                                TickDataSet = string.Format("{0:00}m- {1:00}s", t.Minutes, t.Seconds);
                            }
                            else if (Seconds <= 59)
                            {
                                TickDataSet = t.Seconds + "s";
                            }
                            TickData[i] = TickDataSet;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    try
                    {

                        if (TickData[i] == "1")
                        {
                            string UpTime = RunCommand("ps", "-eo pid,command").Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Contains("-S " + ScreenNames[i])).ToList()[0];
                            UpTime = UpTime.Substring(0, UpTime.IndexOf(" /usr/bin"));
                            UpTime = RunCommand("ps", "-p " + UpTime + " -o etime").Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)[1];
                            UpTime = UpTime.Trim();

                            List<int> UpTimeDate = UpTime.Split(new string[] { "-", ":" }, StringSplitOptions.RemoveEmptyEntries).Select(x => Convert.ToInt32(x)).ToList();
                            while (UpTimeDate.Count < 4)
                            {
                                UpTimeDate.Insert(0, 0);
                            }
                            long Seconds = UpTimeDate[0] * 86400;
                            Seconds += UpTimeDate[1] * 3600;
                            Seconds += UpTimeDate[2] * 60;
                            Seconds += UpTimeDate[3];
                            TimeSpan t = TimeSpan.FromSeconds(Seconds);

                            if (Seconds > 86399)
                            {
                                TickData[i] = String.Format("{0:00}d- {1:00}h", t.Days, t.Hours);
                            }
                            else if (Seconds > 3599)
                            {
                                TickData[i] = String.Format("{0:00}h- {1:00}m", t.Hours, t.Minutes);
                            }
                            else if (Seconds > 59)
                            {
                                TickData[i] = String.Format("{0:00}m- {1:00}s", t.Minutes, t.Seconds);
                            }
                            else if (Seconds <= 59)
                            {
                                TickData[i] = t.Seconds + "s";
                            }
                        }

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(Timestamp + "Error at new Uptime code: \n" + e.Message);
                    }
                }
            }

            WatchCounts.Add("Screen uptime", Watch.ElapsedMilliseconds);

            //Time since backup/Backup size
            string LastBackup = "N/A";
            string[] BackupFileListA = Directory.GetFiles("/home/archive", "SAT*.tar.gz");
            if (!IsProcessRunning(BackupProcess))
            {
                try
                {
                    string LastBackupFileA = BackupFileListA.Last();
                    string[] BackupFileList = Directory.GetFiles("/home/archive", "SAT*.tar.gz");
                    string LastBackupFile = BackupFileList.Last();
                    LastBackupFile = LastBackupFile.Substring(LastBackupFile.IndexOf("-") + 1);
                    LastBackupFile = LastBackupFile.Substring(0, LastBackupFile.IndexOf("."));
                    List<int> LastBackupFileInfo = LastBackupFile.Split('-').Select(x => Convert.ToInt32(x)).ToList();
                    DateTime LastBackupDate = new DateTime(LastBackupFileInfo[0], LastBackupFileInfo[1], LastBackupFileInfo[2], LastBackupFileInfo[3], LastBackupFileInfo[4], 0);
                    TimeSpan TimeSinceLastBackup = DateTime.Now.Subtract(LastBackupDate);
                    if (TimeSinceLastBackup.TotalDays >= 1)
                    {
                        LastBackup = Math.Round(TimeSinceLastBackup.TotalDays, 1) + "d";
                    }
                    else if (TimeSinceLastBackup.TotalHours >= 1)
                    {
                        LastBackup = Math.Round(TimeSinceLastBackup.TotalHours, 1) + "h";
                    }
                    else if (TimeSinceLastBackup.TotalMinutes >= 1)
                    {
                        LastBackup = Math.Round(TimeSinceLastBackup.TotalMinutes, 1) + "m";
                    }
                    else if (TimeSinceLastBackup.TotalSeconds >= 1)
                    {
                        LastBackup = Convert.ToInt32(TimeSinceLastBackup.TotalSeconds) + "s";
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(Timestamp + "Error at Backup time: \n" + e.Message);
                }
            }
            else
            {
                try
                {
                    string LastBackupFileA = BackupFileListA.Last();
                    double Size = Math.Round(Convert.ToDouble(new FileInfo(LastBackupFileA).Length) / 1024.0 / 1024.0 / 1024.0, 2);
                    LastBackup = Size + "GB";
                }
                catch (Exception e)
                {
                    Console.WriteLine(Timestamp + "Error at Backup size: \n" + e.Message);
                }
            }
            WatchCounts.Add("Backup size/time", Watch.ElapsedMilliseconds);


            //Setting server as closing
            try
            {
                Actions.ForEach(x => TickData[(int)x.ServerToClose] = "2");
                string Test = RunCommand("df", "--output=avail / -h");
                Test = Test.Substring(7);
                Test = Test.Substring(0, Test.Length - 1);
                TickData.Add(Test);
            }
            catch (Exception e)
            {
                Console.WriteLine(Timestamp + "Error at TickData closing: \n" + e.Message);
            }
            WatchCounts.Add("Setting closing", Watch.ElapsedMilliseconds);


            try
            {
                string RAMTest = RunCommand("free", "").Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)[1];
                string Total = RAMTest.Substring(RAMTest.IndexOfAny("0123456789".ToCharArray()));
                Total = Total.Substring(0, Total.IndexOf(" "));
                RAMTest = RAMTest.Substring(RAMTest.LastIndexOf(" ") + 1);
                string OutOfRamTest = String.Format("{0:0.00}%", Math.Round(100 - (Convert.ToDouble(RAMTest) / Convert.ToDouble(Total) * 100), 2));
                TickData.Add(OutOfRamTest);
                TickData.Add(CPU.NextValue().ToString());
                TickData.Add(LastBackup);
                TCP.Connections.ForEach(x => x.sendData("Server|poll," + String.Join(",", TickData) + "*"));
            }
            catch (Exception e)
            {
                Console.WriteLine(Timestamp + "Error at sending TickData: " + e.Message);
            }
            WatchCounts.Add("Sending tickdata for " + TCP.Connections.Count, Watch.ElapsedMilliseconds);
            #endregion

            #region Actions
            MsSinceLastTick += (int)T.Interval;
            if (MsSinceLastTick >= 1000)
            {
                try
                {
                    string Existing = String.Join(" - ", Actions.Select(x => x.ServerToClose + ">" + x.ServerToSendTo + "=" + x.Timer + "|" + x.Stage));
                    if (Existing != "")
                    {
                        Console.WriteLine(Timestamp + Existing);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(Timestamp + "Error at Action output: \n" + e.Message);
                }
                MsSinceLastTick = 0;
                foreach (PendingAction A in Actions)
                {
                    switch (A.Stage)
                    {
                        #region MoveServer - 0
                        case 0: //Move Server
                            A.Timer -= 1;
                            if (A.LastAlert == 0)
                            {
                                A.LastAlert = A.Timer + 1;
                            }

                            if (A.Timer == 0)
                            {
                                try
                                {

                                    ///---Closing Proxy---///
                                    if (A.ServerToClose == Server.Proxy)
                                    {
                                        CloseProxy();
                                        if (A.Restart == 1)
                                        {
                                            A.Stage = 2;
                                        }
                                        else
                                        {
                                            A.Stage = 3;
                                        }
                                    }


                                    ///---Closing server with no fallback---///
                                    else if (A.ServerToSendTo == Server.Null)
                                    {
                                        CloseServer(A.ServerToClose);

                                        if (A.Restart == 1)
                                        {
                                            A.Stage = 2;
                                            Console.WriteLine(Timestamp + "Waiting for " + ServerNames[(int)A.ServerToClose] + " to shut down...");
                                        }
                                        else
                                        {
                                            A.Stage = 3;
                                        }
                                    }


                                    ///---Closing server with fallback---///
                                    else
                                    {
                                        if (A.ServerToClose == (Server)4)
                                        {
                                            MovePlayers(Server.Sat, Server.Hub);
                                            MovePlayers(Server.Grind, Server.Hub);
                                        }
                                        else
                                        {
                                            MovePlayers(A.ServerToClose, A.ServerToSendTo);
                                        }
                                        A.Stage = 1;
                                        A.Timer = 10;
                                    }

                                }catch (Exception e)
                                {
                                    Console.WriteLine(Timestamp + "Error in Action 0: \n" + e.Message);
                                }
                            }
                            else if (((A.LastAlert - A.Timer) >= A.AlertInterval && A.AlertInterval != 0) || (A.Timer <= 10 && A.TenSecondCount))
                            {
                                try
                                {
                                    if (A.ServerToClose == Server.Proxy)
                                    {
                                        Say("Full shutdown in " + GetTime(A.Timer), Server.Hub);
                                        Say("Full shutdown in " + GetTime(A.Timer), Server.Sat);
                                        Say("Full shutdown in " + GetTime(A.Timer), Server.Grind);
                                    }
                                    else
                                    {
                                        Say(ServerNames[(int)A.ServerToClose] + " shutdown in " + GetTime(A.Timer), A.ServerToClose);
                                    }
                                    A.LastAlert = A.Timer;
                                }catch (Exception e)
                                {
                                    Console.WriteLine(Timestamp + "Error at announcing action: \n" + e.Message);
                                }
                            }
                            break;
                        #endregion

                        #region Close Server - 1
                        case 1:

                            A.Timer -= 1;
                            if (A.Timer == 0)
                            {
                                try
                                {
                                    if ((int)A.ServerToClose == 4)
                                    {
                                        List<Boolean> Polled = PollScreens();
                                        if (Polled[2]) { CloseServer((Server)2); }
                                        if (Polled[3]) { CloseServer((Server)3); }
                                    }
                                    else
                                    {
                                        CloseServer(A.ServerToClose);
                                    }
                                    if (A.Restart == 1)
                                    {
                                        A.Stage = 2;
                                    }
                                    else
                                    {

                                        if ((int)A.ServerToClose == 4)
                                        {
                                            A.Stage = 0;
                                            A.ServerToClose = Server.Hub;
                                            A.ServerToSendTo = Server.Sat;
                                            A.Timer = 300;
                                            A.AlertInterval = 60;
                                            Console.WriteLine(Timestamp + "Restart was 0");
                                        }
                                        else
                                        {
                                            A.Stage = 3;
                                        }
                                    }
                                }catch(Exception e)
                                {
                                    Console.WriteLine(Timestamp + "Error at Action 1: \n" + e.Message);
                                }
                            }
                            break;
                        #endregion

                        #region Start Server - 2
                        case 2:
                            try
                            {
                                //Console.Write("Trying to start...");
                                if ((int)A.ServerToClose == 4)
                                {
                                    List<Boolean> ServersClosing = new List<bool> { false, false, false };
                                    ServersClosing[1] = StartServer(Server.Sat);
                                    ServersClosing[2] = StartServer(Server.Grind);
                                    if (!ServersClosing.Contains(false))
                                    {
                                        A.Stage = 0;
                                        A.ServerToClose = Server.Hub;
                                        A.ServerToSendTo = Server.Sat;
                                        A.Timer = 300;
                                        A.AlertInterval = 60;
                                    }

                                }
                                else if (StartServer(A.ServerToClose))
                                {
                                    Console.WriteLine(Timestamp + "Done!");
                                    A.Stage = 3;
                                }
                            }
                            catch(Exception e)
                            {
                                Console.WriteLine(Timestamp + "Error at Action 2: \n" + e.Message);
                            }
                            break;
                            #endregion
                    }

                }
                WatchCounts.Add("Actions", Watch.ElapsedMilliseconds);
            }
            try
            {
                Actions = Actions.Where(x => x.Stage != 3).ToList();
            }catch (Exception e)
            {
                Console.WriteLine(Timestamp + "Error at Action 3: \n" + e.Message);
            }
            WatchCounts.Add("Actions cleanup", Watch.ElapsedMilliseconds);
            #endregion

            #region Saving
            if (WrittenToMem)
            {
                try
                {
                    WrittenToMem = false;
                    Mem.SaveMemory("Memory.xml");
                }
                catch (Exception e)
                {
                    Console.WriteLine(Timestamp + "Error at saving memory: \n" + e.Message);
                }
                WatchCounts.Add("Memory saving", Watch.ElapsedMilliseconds);
            }
            #endregion

            #region Tick Performance analysis
            Watch.Stop();
            if(Watch.ElapsedMilliseconds > T.Interval || PerformanceDebug)
            {
                if (!PerformanceDebug)
                {
                    Console.WriteLine(Timestamp + "Unable to keep up, tick took " + Watch.ElapsedMilliseconds);
                }
                else
                {
                    Console.WriteLine(Timestamp + "Performance debugging - " + Watch.ElapsedMilliseconds);
                }
                long MsCount = 0;
                foreach(string Key in WatchCounts.Keys)
                {
                    long Value = 0;
                    WatchCounts.TryGetValue(Key, out Value);
                    Console.WriteLine("Action: {0} took {1}ms", Key, Value - MsCount);
                    MsCount = Value;
                }
            }
            #endregion
        }
        #endregion

        #region Functions
        static void GetArg()
        {
            BackupProcess.StartInfo.FileName = "tar";
            string DateFormat = RunCommand("date", "\'+%Y-%m-%d-%H-%M\'").Replace("\n", "");
            BackupProcess.StartInfo.WorkingDirectory = "/home/archive";
            BackupProcess.StartInfo.Arguments = "-zcvf \"SAT-" + DateFormat + ".tar.gz\" database.sql /home/SAT";
        }

        static Boolean IsProcessRunning(Process process)
        {
            if (!FirstBackupDone || process.HasExited)
            {
                return false;
            }
            return true;
        }

        static void MovePlayers(Server From, Server To)
        {
            RunCommand("screen", "-S " + ScreenNames[0] + " -p 0 -X stuff \"^Msend {0} {1}^M\"", ServerNames[(int)From].ToLower(), ServerNames[(int)To].ToLower());
        }

        static void CloseProxy()
        {
            RunCommand("screen", "-S " + ScreenNames[0] + " -p 0 -X stuff \"^Mend^M\"");
        }

        static string GetTime(int Timer)
        {
            TimeSpan t = TimeSpan.FromSeconds(Timer);
            if (Timer > 86399)
            {
                return String.Format("{0}d, {1}h", t.Days, t.Hours);
            }
            else if (Timer > 3599)
            {
                return String.Format("{0}h, {1}m, {2}s", t.Hours, t.Minutes, t.Seconds);
            }
            else if (Timer > 59)
            {
                return String.Format("{0}m, {1}s", t.Minutes, t.Seconds);
            }
            else if (Timer <= 59)
            {
                return t.Seconds + "s";
            }
            return "";
        }

        static string RunCommand(string Command, string Arg, params string[] Format)
        {
            Arg = String.Format(Arg, Format);
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = Command;
            p.StartInfo.Arguments = Arg;
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output;
        }
        static void CloseServer(Server server)
        {
            RunCommand("screen", "-S " + ScreenNames[(int)server] + " -p 0 -X stuff \"^Mstop^M\"");
        }

        static void Announce(string Title, string Text, int MessageTimer)
        {
            RunCommand("screen", "-S {0} -p 0 -X stuff \"^Mtb {1} &4{2} &8{3}^M\"", ScreenNames[0], MessageTimer.ToString(), Title, Text);
        }
     
        static void Say(string Text, Server server)
        {
            RunCommand("screen", "-S {0} -p 0 -X stuff \"^Msay {1}^M\"", ScreenNames[(int)server], Text);
        }

        static Boolean StartServer(Server server)
        {
            int ServerNum = (int)server;
            if (ScreenExists(ScreenNames[ServerNum]))
            {
                return false;
            }
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.WorkingDirectory = String.Format("/home/SAT/{0}/server", Folders[ServerNum]);
            p.StartInfo.FileName = "screen";
            p.StartInfo.Arguments = String.Format("-dm -S {0} java -jar server.jar", ScreenNames[ServerNum]);
            p.Start();
            return true;
        }


        static Boolean ScreenExists(string Name)
        {
            string Output = RunCommand("screen", "-list");
            if (Output.Contains(Name))
            {
                return true;
            }
            return false;
        }

        static List<Boolean> PollScreens()
        {
            string Output = RunCommand("screen", "-list");
            List<Boolean> Returning = new List<bool>();
            foreach(string a in ScreenNames)
            {
                Returning.Add(Output.Contains(a));
            }
            Returning.Add(true);
            return Returning;
        }
        #endregion

        #region Lists
        static List<string> ScreenNames = new List<string>() 
        { 
            "Proxy",
            "HubServer",
            "SatServer",
            "GrindServer"
        };

        static List<string> Folders = new List<string>()
        {
            "1",
            "2",
            "3",
            "4"
        };

        static List<string> ServerNames = new List<string>()
        {
            "Server",
            "Hub",
            "Sat",
            "Grind",
            "All-Worlds",
        };

        static List<string> FallbackMessages = new List<string>()
        {
            "Error: 1",
            "Sending players to Hub",
            "Sending players to Sat",
            "Sending players to Grind",
            "No fallback",
            "Error: 2"
        };

        static List<string> RestartMessages = new List<string>()
        {
            "{0}-Reset!",
            "{0}-Auto-Reset!"
        };

        public enum Server{
            Proxy = 0,
            Hub = 1,
            Sat = 2,
            Grind = 3,
            Null = 4
        }
        #endregion
    }

    #region Memory
    public class Memory : GSMemory
    {
        public List<LoggedAction> LoggedActions = new List<LoggedAction>();
    }

    public class LoggedAction
    {
        string datetime = "";
        public DateTime dateTime
        {
            get 
            { 
                return DateTime.Parse(datetime, null, System.Globalization.DateTimeStyles.None);
            }
            set
            {
                datetime = value.ToString("s");
            }
        }
        public string User = "";
        public int TargettedServer = -1;
    }
    #endregion

    #region Tick TCP
    public class PendingAction
    {
        public int Timer = 0;
        public int AlertInterval = 0;
        public int LastAlert = 0;
        public int Stage = 0; //0 = Sending, 1 = Stopping, 2 = Starting, 3 = Clean up
        public int Restart = 1;
        public MainClass.Server ServerToClose;
        public MainClass.Server ServerToSendTo;
        public Boolean TenSecondCount = false;
        public int Kill = 0;
        public string User = "";
    }

    public class PendingString
    {
        public string Send = "";
        public TcpServerConnection WhoToSendTo;
    }
    #endregion

    #region AuthCrypt
    public static class AuthCrypt
    {
        // This size of the IV (in bytes) must = (keysize / 8).  Default keysize is 256, so the IV must be
        // 32 bytes long.  Using a 16 character string here gives us 32 bytes when converted to a byte array.
        private const string initVector = "skw27vhm8j5p8ll3";
        // This constant is used to determine the keysize of the encryption algorithm
        private const int keysize = 256;
        //Encrypt
        public static string EncryptString(string plainText, string passPhrase)
        {
            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.Mode = CipherMode.CBC;
            ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
            byte[] cipherTextBytes = memoryStream.ToArray();
            memoryStream.Close();
            cryptoStream.Close();
            return Convert.ToBase64String(cipherTextBytes);
        }
        //Decrypt
        public static string DecryptString(string cipherText, string passPhrase)
        {
            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
            byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.Mode = CipherMode.CBC;
            ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream(cipherTextBytes);
            CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            byte[] plainTextBytes = new byte[cipherTextBytes.Length];
            int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
            memoryStream.Close();
            cryptoStream.Close();
            return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
        }
    }
    #endregion
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SimpleTCP;
using System.Net.Sockets;
using Alba.CsConsoleFormat;

namespace SATClient
{
    public class MainClass
    {
        public static Boolean DebugMode = false;
        public static string VersionNumber = "1.31";
        public static Boolean Experimental;

        #region Main
        public static void Main(string[] args)
        {
            System.Threading.Thread.Sleep(1000);
            Experimental = args.Length != 0 && args[0] == "true";
            Console.Clear();
            if (Experimental)
            {
                Console.WriteLine("{0} {1}", Console.BufferWidth, Console.BufferHeight);
                foreach(ConsoleColor col in Enum.GetValues(typeof(ConsoleColor)))
                {
                    Console.ForegroundColor = col;
                    Console.WriteLine($"Colour is {col}");
                }
                Console.ReadLine();
            }

            if(Console.BufferWidth < 44 || Console.BufferHeight < 21)
            {
                Console.WriteLine("Console screen too small");
                Console.WriteLine("Required Buffer size: 44 x 20");
                Console.WriteLine("Your Buffer size: {0} x {1}", Console.BufferWidth, Console.BufferHeight);
                Console.WriteLine("Lowering the font size can help");
                Console.WriteLine("An Admin can help");
                Console.ReadKey();
            }
            Console.ForegroundColor = ConsoleColor.White;
            for (int i = 0; i < Console.WindowHeight; i++)
            {
                Console.Write(new string(' ', Console.WindowWidth));
            }
            StatusMenu Rawr2 = new StatusMenu(Console.WindowWidth - 16, 0);
            Console.CursorVisible = false;
            SelectMenu Rawr = new SelectMenu(0, 0, Rawr2);
        }
        #endregion
    }

    #region StatusMenu
    public class StatusMenu
    {
        public Boolean Draw
        {
            get
            {
                return draw;
            }
            set
            {
                draw = value;
                Redraw(value);
            }
        }
        Boolean draw = true;
        public int X = 0, Y = 0;
        List<string> Names = new List<string>() { "Proxy", "Hub", "Sat", "Grind", "Drive", "RAM", "CPU", "Backup" };
        List<string> CurrentValues = new List<string>() { "", "", "", "", "", "", "", "" };
        public StatusMenu(int X, int Y)
        {
            //X += 2;
            Y += 2;
            this.X = X;
            this.Y = Y;
            Console.SetCursorPosition(X + 3, Y - 1);
            Console.Write("-Status-");
            Console.SetCursorPosition(X, Y);
            for (int i = 0; i < Names.Count; i++)
            {
                Console.SetCursorPosition(X, Y + i);
                Console.Write(Names[i] + ":");
            }
            Console.ForegroundColor = ConsoleColor.White;

        }

        public void Redraw(Boolean Value)
        {
            try
            {
                for (int i = -1; i < 8; i++)
                {
                    Console.SetCursorPosition(this.X, this.Y + i);
                    if (Value)
                    {
                        Console.Write(Names[i] + ":");
                    }
                    else
                    {
                        Console.WriteLine(new string(' ', 16));
                    }
                }
            }
            catch (Exception e)
            {

            }
        }

        public void RenderStatus(List<string> Vals)
        {
            if (draw)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (Vals[i].ToString() == CurrentValues[i]) { continue; }
                    Console.SetCursorPosition(X + 16 - 8, Y + i);
                    Console.Write(new string(' ', 8));
                    List<string> Accepted = new List<string>() { "0", "2", "3", "4" };
                    if (Accepted.Contains(Vals[i]))
                    {
                        string TextToWrite = StatusMessages[Convert.ToInt32(Vals[i])];
                        Console.SetCursorPosition(X + 16 - TextToWrite.Length, Y + i);
                        Console.ForegroundColor = StatusColours[Convert.ToInt32(Vals[i])];
                        Console.Write(TextToWrite);
                    }
                    else
                    {
                        string TextToWrite = Vals[i].Replace('-', ',');
                        Console.SetCursorPosition(X + 16 - TextToWrite.Length, Y + i);
                        Console.ForegroundColor = StatusColours[1];
                        Console.WriteLine(TextToWrite);
                    }

                    CurrentValues[i] = Vals[i];
                }
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public void RenderStats(List<string> Stats)
        {
            if (draw)
            {
                Stats[0] += "B";
                //Stats[1] += "B";
                Stats[2] = String.Format("{0:0.00}%", Math.Round(Convert.ToDouble(Stats[2]), 2));
                for (int i = 0; i < Stats.Count; i++)
                {
                    if (Stats[i] == CurrentValues[i + 4]) { continue; }
                    Console.SetCursorPosition(X + 16 - 9, Y + 4 + i);
                    Console.Write(new string(' ', 9));
                    if (Stats[i].Length > 9)
                    {
                        Stats[i] = Stats[i].Substring(0, 9);
                    }
                    Console.SetCursorPosition(X + 16 - Stats[i].Length, Y + 4 + i);
                    Console.ForegroundColor = StatusColours[5];
                    Console.Write(Stats[i]);
                    Console.ForegroundColor = ConsoleColor.White;
                    CurrentValues[i + 4] = Stats[i];
                }
            }
        }

        List<string> StatusMessages = new List<string>
        {
            "Offline",
            "Online",
            "Closing",
            "Starting",
            "Disabled"
        };

        List<ConsoleColor> StatusColours = new List<ConsoleColor>
        {
            ConsoleColor.Red,
            ConsoleColor.Green,
            ConsoleColor.Blue,
            ConsoleColor.Blue,
            ConsoleColor.DarkGray,
            ConsoleColor.Cyan, //For Drive Space
        };
    }
    #endregion

    #region SelectMenu
    public enum InputType
    {
        Key,
        Text,
        TextNumbers
    }

    public class SelectMenu
    {
        #region Variables
        public int X = 0, Y = 0;
        public int Width = 0, Height = 0;
        InputType CurrentInputType = InputType.Key;
        List<string> ErrorArgs = new List<string>() { "Success", "Action already exists", "No fallback", "Alternative fallback used", "Disabled", "Not allowed", "Backup already in progress" };//Restart temporarily disabled for this server
        List<List<MenuItem>> Menus = new List<List<MenuItem>>();
        List<int> AllowedRestarts = new List<int>() { 1, 1, 1, 1 };
        public List<List<MenuItem>> GenerateNewList()
        {
            List<List<MenuItem>> MenusA = new List<List<MenuItem>>()
        {
            new List<MenuItem>() //0 - Main Menu
            {
                new Header(){ Text = "---Restart---"},
                new SelectMenuOption(){ MenuID = 1, Selected = true, Text = "Proxy"},
                new SelectMenuOption(){ MenuID = 1, Selected = false, Text = "Hub"},
                new SelectMenuOption(){ MenuID = 1, Selected = false, Text = "Sat"},
                new SelectMenuOption(){ MenuID = 1, Selected = false, Text = "Grind"},
                //new SelectMenuOption(){ MenuID = 1, Selected = false, Text = "All Worlds"},
                new SelectMenuOption(){ MenuID = 3, IDInMenu = 9, Selected = false, Text = "Cancel restarts"},
                new Header(){ Text = ""},
                new Header(){ Text = "---Extras---"},
                new SelectMenuOption(){MenuID = 3, IDInMenu = 7, Selected = false, Text = "Troubleshooting"},
                new SelectMenuOption{ MenuID = 3, IDInMenu = 8, Selected = false, Text = "Create backup"}
            },
            new List<MenuItem>() //1 - Restart Menu
            {
                new Header{ Text = "---Restart | {SERVER}---"},
                new ToggleMenuOption{ Text = "Toggle server", Selected = true},
                new ToggleMenuOption{ Text = "Kill server", Selected = false},
                new SelectMenuOption(){ MenuID = 3, IDInMenu = 6, Selected = false, Text = "Quick (10s)", Information = new Dictionary<string, string>()
                    {
                        {"Timer", "10" },
                        {"TenSecondAlert", "0"},
                        {"Announce", "1"},
                        {"Fallback", "0"},
                        {"Alert", "0"},
                    }
                },
                new SelectMenuOption(){ MenuID = 3, IDInMenu = 6, Selected = false, Text = "Delayed (5m)", Information = new Dictionary<string, string>()
                    {
                        {"Timer", "300"},
                        {"TenSecondAlert", "1"},
                        {"Announce", "1"},
                        {"Fallback", "0"},
                        {"Alert", "60"},
                    }
                },
                //new SelectMenuOption(){ MenuID = 2, Selected = false, Text = "Advanced"},
                new SelectMenuOption {MenuID = 3, IDInMenu = 7, Selected = false, Text = "Back"},
            },
            new List<MenuItem>()//2 - Advanced menu
            {
                new Header{ Text = "Timer"},
                new TextBoxMenuOption() { Selected = true },
                new Header(),
                new Header{ Text = "---Ten Second Alert---"},
                new ToggleMenuOption(){ Text = "Enabled"},
                new Header(),
                new Header{ Text = "---Announce on Screen---"},
                new ToggleMenuOption(){ Text = "Enabled"},
                new Header(),
                new Header{Text = "---Fallback server---"},
                new Header{Text = "Insert multi-toggle"}
            },
            new List<MenuItem>()//3 - Contact server
            {
                new Header{ Text = "Contacting server..."}
            },
            new List<MenuItem>(),//4 - Display reply 
            new List<MenuItem>//5 - Analytics
            {
                new SelectMenuOption{MenuID = 3, IDInMenu = 12, Selected = true, Text = "Restarts/hr"},
                new SelectMenuOption{MenuID = 3, IDInMenu = 12, Selected = false, Text = "User log"},
                new SelectMenuOption{MenuID = 3, IDInMenu = 12, Selected = false, Text = "Back"}//─
            },

        };
            if (MainClass.Experimental)
            {
                MenusA[0].AddRange(new List<MenuItem>
                {
                    new SelectMenuOption{ Text = "Analytics", MenuID = 3, IDInMenu = 11}
                });
            }

            if (Environment.UserName == "root" || MainClass.DebugMode)
            {
                MenusA[0].AddRange(new List<MenuItem>
                {
                    new Header{ Text = ""},
                    new Header{ Text = "---Root Access---"},
                    new ToggleMenuOption{ Text = "Disable Proxy restart", IDInMenu = 1, Toggled = AllowedRestarts[0] == 1 ? false : true},
                    new ToggleMenuOption{ Text = "Disable Hub restart", IDInMenu = 2, Toggled = AllowedRestarts[1] == 1 ? false : true},
                    new ToggleMenuOption{ Text = "Disable Sat restart", IDInMenu = 3, Toggled = AllowedRestarts[2] == 1 ? false : true},
                    new ToggleMenuOption{ Text = "Disable Grind restart", IDInMenu = 4, Toggled = AllowedRestarts[3] == 1 ? false : true},
                    new SelectMenuOption{ Text = "Apply", MenuID = 3, IDInMenu = 5}
                });
            }
            return MenusA;
        }

        //IDInMenus
        //0 = Null
        //1 = Disable Proxy Restart
        //2 = Disable Hub Restart
        //3 = Disable Sat Restart
        //4 = Disable Grind Restart
        //5 = Apply Restart Rights
        //6 = Restart
        //7 = Reset Menu
        //8 = Backup
        //9 = Cancel Restarts
        //10 = Quit
        //11 = Open Analytics
        //12 = Close Analytics


        List<MenuItem> CurrentMenu = new List<MenuItem>();
        StatusMenu statusMenu;
        SimpleTcpClient TCPClient = new SimpleTcpClient();
        Boolean FirstRender = true;
        System.Timers.Timer DisconnectTimer;
        #endregion

        public SelectMenu(int X, int Y, StatusMenu statusMenu)
        {
            X++;
            Y++;
            this.X = X;
            this.Y = Y;
            this.statusMenu = statusMenu;
            TCPClient.DataReceived += TCPClient_DataReceived;
            try
            {
                TCPClient.Connect("localhost", 2010);
            }
            catch (SocketException e)
            {
                Console.Clear();
                Console.WriteLine("Unable to connect to server");
            }

            DisconnectTimer = new System.Timers.Timer()
            {
                Interval = 5000, AutoReset = true
            };
            DisconnectTimer.Elapsed += DisconnectTimer_Elapsed;

            while (true)
            {
                try
                {
                    switch (CurrentInputType)
                    {
                        case InputType.Key:
                            KeyPressed(Console.ReadKey(true));
                            break;

                        case InputType.Text:
                            string InputText = Console.ReadLine();
                            List<MenuItem> SelectOptions = new List<MenuItem>(CurrentMenu.Where(x => x.Selected));
                            MenuItem Focused = SelectOptions[0];
                            Focused.Text = InputText;
                            CurrentInputType = InputType.Key;
                            //MenuItem Focused = new List<MenuItem>(CurrentMenu.Where(x => x.Selected).ToList))[0];
                            break;
                    }
                }catch (ArgumentOutOfRangeException e)
                {
                    TCPClient.Disconnect();
                    Console.Clear();
                    switch (e.ParamName)
                    {
                        case "top":
                            Console.WriteLine("Error on Variable: Top\nThis is caused by a font size that is too large.");
                            break;
                    }
                }
            }
        }

        public void RenderMenu(int ID, int Selected = -1)
        {
            #region Setup
            Dictionary<string, string> Info = new Dictionary<string, string>();
            try
            {
                Info = CurrentMenu.Where(x => x.Selected).ToList()[0].Information;
            }
            catch
            {

            }
            for (int i = 0; i < Height; i++)
            {
                Console.SetCursorPosition(X, Y + i);
                Console.Write(new string(' ', Width));
            }
            List<MenuItem> PreviousMenu = CurrentMenu;
            CurrentMenu = Menus[ID];
            switch (ID)
            {
                case 1:
                    CurrentMenu[0].Text = "---Restart | " + ServerNames[Selected] + "---";
                    break;

                case 5:
                    int Offset = 0;
                    //Console.SetCursorPosition(Offset, 2);
                    //Console.Write("Rawr");
                    /*List<char> Name = "Lana".ToCharArray().ToList();
                    for(int i = 0; i < Name.Count; i++)
                    {
                        Console.SetCursorPosition(Offset + 1 + (i * 1), 20 - 5 + i);
                        Console.Write(Name[i]);
                    }*/

                    //Console.Write("L\n\t\tA\n\t\t\t\tN\a\t\t\t\t\t\tA");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.SetCursorPosition(Offset, 0);

                    ConsoleBuffer buffer = new ConsoleBuffer(width: Console.BufferWidth);
                    bool Blocks = true;
                    if (false)
                    {
                        for (int i = 2; i < 6; i++)
                        {
                            if (Blocks)
                            {
                                buffer.FillBackgroundHorizontalLine(3, i, 3, ConsoleColor.Red);
                            }
                            else
                            {
                                buffer.DrawHorizontalLine(3, i, 3, ConsoleColor.Red);
                            }
                        }
                        for (int i = 6; i < 11; i++)
                        {
                            if (Blocks)
                            {
                                buffer.FillBackgroundHorizontalLine(3, i, 3, ConsoleColor.Yellow);
                            }
                            else
                            {
                                buffer.DrawHorizontalLine(3, i, 3, ConsoleColor.Yellow);
                            }
                        }
                        for (int i = 11; i < 16; i++)
                        {
                            if (Blocks)
                            {
                                buffer.FillBackgroundHorizontalLine(3, i, 3, ConsoleColor.Green);
                            }
                            else
                            {
                                buffer.DrawHorizontalLine(3, i, 3, ConsoleColor.Green);
                            }
                        }
                        buffer.DrawString(0, 15, ConsoleColor.White, " 0-");
                        buffer.DrawString(0, 5, ConsoleColor.White, "20-");
                        buffer.DrawString(0, 10, ConsoleColor.White, "10-");
                        buffer.DrawString(0, 0, ConsoleColor.White, "40-");

                        buffer.DrawString(4, 16, ConsoleColor.White, "L");
                        buffer.DrawString(5, 17, ConsoleColor.White, "a");
                        buffer.DrawString(6, 18, ConsoleColor.White, "n");
                        buffer.DrawString(7, 19, ConsoleColor.White, "a");
                        buffer.DrawString(4, 1, ConsoleColor.White, "39");
                    }
                    else
                    {
                        buffer.DrawString(2, 8, ConsoleColor.White, "Lana");
                        buffer.FillBackgroundHorizontalLine(9, 8, 10, ConsoleColor.Green);
                        buffer.FillBackgroundHorizontalLine(19, 8, 10, ConsoleColor.Yellow);
                        buffer.FillBackgroundHorizontalLine(29, 8, 10, ConsoleColor.Red);

                        buffer.DrawString(40, 8, ConsoleColor.White, "30");
                    }

                    new ConsoleRenderTarget().Render(buffer);

                    for(int i = 0; i < 6; i++)
                    {
                        //Console.SetCursorPosition(Offset, Console.BufferHeight - 7 - i - 1);
                        //Encoding A = Console.OutputEncoding;
                        //Console.OutputEncoding = Encoding.GetEncoding(866);
                        //Console.Write("───");
                        //Console.OutputEncoding = A;
                    }
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }
            Width = CurrentMenu.Select(x => x.Text.Length).ToList<int>().Max() + 6;
            if (CurrentMenu.OfType<Header>().Count() > 0) {
                int Width2 = CurrentMenu.OfType<Header>().Select(x => x.Text.Length / 2 + 16).Max();
            if (Width2 > Width) { Width = Width2; }
            }
            Height = 2 + CurrentMenu.Count;
            for (int i = 0; i < Height; i++)
            {
                Console.SetCursorPosition(X, Y + i - 1);
                Console.Write(new string(' ', Width));
            }
            #endregion

            #region Rendering
            switch (ID)
            {
                case 3:
                    int PreviousMenuIDInMenu = PreviousMenu.Where(x => x.Selected).ToList()[0].IDInMenu;
                    switch (PreviousMenuIDInMenu)
                    {

                        #region Root Access
                        case 5:
                            List<int> RootRestartAccess = PreviousMenu.Where(x => new int[] { 1, 2, 3, 4 }.Contains(x.IDInMenu)).Select(x => (x as ToggleMenuOption).Toggled ? 0 : 1).ToList();
                            string SendRoot = String.Format("{0}|killswitch,{1}", Environment.UserName, String.Join(",", RootRestartAccess));
                            TCPClient.WriteLine(SendRoot);
                            break;
                        #endregion

                        #region Restart server
                        case 6:
                            int SelectedServer = GetSelectedIndex(0);
                            List<string> Values = new List<string>(Info.Select(x => x.Value));
                            Values.Add((PreviousMenu.Where(x => x.Text == "Toggle server").ToList()[0] as ToggleMenuOption).Toggled ? "0" : "1");//Restart
                            Values.Add((PreviousMenu.Where(x => x.Text == "Kill server").ToList()[0] as ToggleMenuOption).Toggled ? "1" : "0");//Kill
                            string SendRestart = String.Format("{0}|restart,{1},{2}", Environment.UserName, SelectedServer, String.Join(",", Values));//,{3},{4},{5},{6}
                            TCPClient.WriteLine(SendRestart);
                            break;
                        #endregion

                        #region Reset menu
                        case 7:
                            Menus = GenerateNewList();
                            RenderMenu(0);
                            break;
                        #endregion

                        #region Backup
                        case 8:
                            TCPClient.WriteLine(String.Format("{0}|backup", Environment.UserName));
                            break;
                        #endregion

                        #region Cancel Restarts
                        case 9:
                            TCPClient.WriteLine(String.Format("{0}|stoprestart", Environment.UserName));
                            break;
                        #endregion

                        #region Quit
                        case 10:
                            Environment.Exit(0);
                            break;
                        #endregion

                        #region Open Analytics
                        case 11:
                            statusMenu.Draw = false;
                            RenderMenu(5);
                            break;
                        #endregion

                        #region Close Analytics
                        case 12:
                            statusMenu = new StatusMenu(Console.WindowWidth - 16, 0);
                            Menus = GenerateNewList();
                            RenderMenu(0);
                            break;
                        #endregion
                    }

                    break;
                default:
                    for (int i = 0; i < this.CurrentMenu.Count; i++)
                    {
                        Console.ForegroundColor = CurrentMenu[i].Colour;
                        switch (CurrentMenu[i].GetType().ToString())
                        {
                            case "SATClient.SelectMenuOption":
                                Console.SetCursorPosition(X + 1, Y + i);
                                switch ((CurrentMenu[i] as SelectMenuOption).Selected)
                                {
                                    case true:
                                        Console.Write("[*] " + (CurrentMenu[i] as SelectMenuOption).Text);
                                        break;
                                    case false:
                                        Console.Write("[ ] " + (CurrentMenu[i] as SelectMenuOption).Text);
                                        break;
                                }
                                break;
                            case "SATClient.Header":
                                int TryPositon = X + 15 - (CurrentMenu[i].Text.Length / 2);
                                Console.SetCursorPosition(TryPositon > -1 ? TryPositon : 1, Y + i);
                                Console.Write(CurrentMenu[i].Text);
                                break;
                            case "SATClient.ToggleMenuOption":
                                Console.SetCursorPosition(X + 1, Y + i);
                                switch((CurrentMenu[i] as ToggleMenuOption).Toggled)
                                {
                                    case true:
                                        Console.Write("(X) " + (CurrentMenu[i] as ToggleMenuOption).Text);
                                        break;

                                    case false:
                                        switch ((CurrentMenu[i] as ToggleMenuOption).Selected)
                                        {
                                            case true:
                                                Console.Write("(*) " + (CurrentMenu[i] as ToggleMenuOption).Text);
                                                break;
                                            case false:
                                                Console.Write("( ) " + (CurrentMenu[i] as ToggleMenuOption).Text);
                                                break;
                                        }
                                        break;
                                }

                                break;
                            case "SATClient.TextBoxMenuOption":
                                Console.SetCursorPosition(X + 5, Y + i);
                                if (!CurrentMenu[i].Selected)
                                {
                                    Console.ForegroundColor = ConsoleColor.DarkGray;
                                }
                                Console.Write(">" + (CurrentMenu[i] as TextBoxMenuOption).Text);
                                break;
                        }
                    }
                    break;
            }

            Console.ForegroundColor = ConsoleColor.White;
            #endregion
        }

        int GetSelectedIndex(int ID)
        {
            return Menus[ID].Where(x => x.GetType().ToString() == "SATClient.SelectMenuOption"
            || x.GetType().ToString() == "SATClient.ToggleMenuOption"
            || x.GetType().ToString() == "SATClient.TextBoxMenuOption").ToList().IndexOf(Menus[ID].Where(x => x.Selected).ToList()[0]);
        }

        void TCPClient_DataReceived(object sender, Message e)
        {

            List<string> Messages = e.MessageString.Split(new string[] { "*" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (string text in Messages)
            {
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

                switch (Command)
                {
                    #region ShowError
                    case "ShowError":
                        //Console.WriteLine("Received");
                        try
                        {
                            Menus[4].Clear();
                            foreach (string a in Args)
                            {
                                Menus[4].Add(new Header() { Text = ErrorArgs[Convert.ToInt32(a)] });
                            }
                            Menus[4].Add(new SelectMenuOption() { Text = "Main menu", MenuID = 3, IDInMenu = 7, Selected = true });
                            RenderMenu(4);
                        }
                        catch (Exception e1)
                        {
                            Console.WriteLine("It broke, tell Lana Avery that the tick thingy broke...");
                        }
                        break;
                    #endregion

                    #region Poll
                    case "poll":
                        try
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                AllowedRestarts[i] = Args[i] == "4" ? 0 : 1;
                            }
                            statusMenu.RenderStatus(Args.GetRange(0, 4));
                            statusMenu.RenderStats(Args.GetRange(5, 4));
                            if (FirstRender)
                            {
                                Menus = GenerateNewList();
                                FirstRender = false;
                                RenderMenu(0);
                            }
                            DisconnectTimer.Stop();
                            DisconnectTimer.Start();
                        }
                        catch (Exception e1)
                        {

                        }
                        break;
                    #endregion

                    #region Killswitch
                    case "killswitch":
                        RenderMenu(0);
                        break;
                    #endregion

                    #region VersionData
                    case "versiondata":
                        if (Args[0] != MainClass.VersionNumber)
                        {
                            Double currVer = Convert.ToDouble(MainClass.VersionNumber);
                            Double servVer = Convert.ToDouble(Args[0]);
                            Menus[4].Clear();
                            if (currVer > servVer)
                            {
                                Menus[4].Add(new Header() { Text = "Menu version is higher than Server version." });
                            }
                            else
                            {
                                Menus[4].Add(new Header() { Text = "Server version is higher than Menu version." });
                            }
                            Menus[4].Add(new SelectMenuOption() { Text = "Exit", MenuID = 3, IDInMenu = 10, Selected = true });
                            RenderMenu(4);
                        }
                        else if (ConnectionTries >= 1)
                        {
                            ConnectionTries = 0;
                            Menus = GenerateNewList();
                            RenderMenu(0);
                        }
                        break;
                        #endregion
                }
            }
        }
        int ConnectionTries = 0;
        void DisconnectTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                ConnectionTries++;
                TCPClient.Dispose();
                TCPClient = new SimpleTcpClient();
                TCPClient.DataReceived += TCPClient_DataReceived;
                TCPClient.Connect("localhost", 2010);
            }
            catch
            {

            }
            if (ConnectionTries == 1)
            {
                try
                {
                    Menus[4].Clear();
                    Menus[4].Add(new Header { Text = "Connection lost!" });
                    Menus[4].Add(new Header { Text = "Retrying..." });
                    Menus[4].Add(new SelectMenuOption() { Text = "Exit", MenuID = 3, IDInMenu = 10, Selected = true });
                    RenderMenu(4);
                }catch(Exception e1)
                {

                }
            }
        }

        public void KeyPressed(ConsoleKeyInfo keyInfo)
        {
            //Console.BackgroundColor = ChosenColour;
            //List<SelectMenuOption> SelectOptions = new List<SelectMenuOption>(CurrentMenu.OfType<SelectMenuOption>());
            List<MenuItem> SelectOptions = new List<MenuItem>(CurrentMenu.Where(x => x.GetType().ToString() == "SATClient.SelectMenuOption"
            || x.GetType().ToString() == "SATClient.ToggleMenuOption" 
            || x.GetType().ToString() == "SATClient.TextBoxMenuOption"));
            int CurrentlySelected = CurrentMenu.IndexOf(SelectOptions.Where(x => x.Selected).ToList()[0]); //Place in Menu
            int InternalSelected = SelectOptions.IndexOf(SelectOptions.Where(x => x.Selected).ToList()[0]); //Button ID
            switch (keyInfo.Key)
            {
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    MenuItem Next = new MenuItem();
                    int NextSelect = 0;

                    if (InternalSelected != SelectOptions.Count - 1)
                    {
                        Next = SelectOptions[InternalSelected + 1];
                        NextSelect = CurrentMenu.IndexOf(Next);
                    }
                    else
                    {
                        Next = SelectOptions.First();
                        NextSelect = CurrentMenu.IndexOf(Next);
                    }
                    (CurrentMenu[CurrentlySelected] as MenuItem).Selected = false;
                    (CurrentMenu[NextSelect] as MenuItem).Selected = true;

                    switch (CurrentMenu[CurrentlySelected].GetType().ToString())
                    {
                        case "SATClient.SelectMenuOption":
                            Console.SetCursorPosition(X + 2, Y + CurrentlySelected);
                            Console.Write(" ");
                            break;

                        case "SATClient.ToggleMenuOption":
                            Console.SetCursorPosition(X + 2, Y + CurrentlySelected);
                            if ((CurrentMenu[CurrentlySelected] as ToggleMenuOption).Toggled)
                            {

                                Console.Write("X");
                            }
                            else
                            {
                                Console.Write(" ");
                            }
                            break;
                        case "SATClient.TextBoxMenuOption":
                            Console.SetCursorPosition(X + 5, Y + CurrentlySelected);
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write(">" + (CurrentMenu[CurrentlySelected] as TextBoxMenuOption).Text);
                            Console.ForegroundColor = ConsoleColor.White;
                            break;
                    }

                    switch (Next.GetType().ToString())
                    {
                        case "SATClient.SelectMenuOption":
                            Console.SetCursorPosition(X + 2, Y + NextSelect);
                            Console.Write("*");
                            break;

                        case "SATClient.ToggleMenuOption":
                            Console.SetCursorPosition(X + 2, Y + NextSelect);
                            Console.Write("*");
                            break;

                        case "SATClient.TextBoxMenuOption":
                            Console.SetCursorPosition(X + 2, Y + NextSelect);
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Write(">" + Next.Text);
                            break;
                    }

                    break;

                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    MenuItem Prev = new MenuItem();
                    int PrevSelect = 0;
                    if (InternalSelected != 0)
                    {
                        Prev = SelectOptions[InternalSelected - 1];
                        PrevSelect = CurrentMenu.IndexOf(Prev);
                    }
                    else
                    {
                        Prev = SelectOptions.Last();
                        PrevSelect = CurrentMenu.IndexOf(Prev);
                    }
                    CurrentMenu[CurrentlySelected].Selected = false;
                    CurrentMenu[PrevSelect].Selected = true;

                    switch (CurrentMenu[CurrentlySelected].GetType().ToString())
                    {
                        case "SATClient.SelectMenuOption":
                            Console.SetCursorPosition(X + 2, Y + CurrentlySelected);
                            Console.Write(" ");
                            break;

                        case "SATClient.ToggleMenuOption":
                            Console.SetCursorPosition(X + 2, Y + CurrentlySelected);
                            if ((CurrentMenu[CurrentlySelected] as ToggleMenuOption).Toggled)
                            {

                                Console.Write("X");
                            }
                            else
                            {
                                Console.Write(" ");
                            }
                            break;
                        case "SATClient.TextBoxMenuOption":
                            Console.SetCursorPosition(X + 5, Y + CurrentlySelected);
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write(">" + (CurrentMenu[CurrentlySelected] as TextBoxMenuOption).Text);
                            Console.ForegroundColor = ConsoleColor.White;
                            break;
                    }

                    switch (Prev.GetType().ToString())
                    {
                        case "SATClient.SelectMenuOption":
                            Console.SetCursorPosition(X + 2, Y + PrevSelect);
                            Console.Write("*");
                            break;

                        case "SATClient.ToggleMenuOption":
                            Console.SetCursorPosition(X + 2, Y + PrevSelect);
                            Console.Write("*");
                            break;

                        case "SATClient.TextBoxMenuOption":
                            Console.SetCursorPosition(X + 5, Y + PrevSelect);
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Write(">" + Prev.Text);
                            break;
                    }

                    break;

                case ConsoleKey.Enter:
                    switch (CurrentMenu[CurrentlySelected].GetType().ToString())
                    {
                        case "SATClient.SelectMenuOption":
                            RenderMenu(SelectOptions[InternalSelected].MenuID, InternalSelected);
                            break;

                        case "SATClient.ToggleMenuOption":
                            (CurrentMenu[CurrentlySelected] as ToggleMenuOption).Toggled = !(CurrentMenu[CurrentlySelected] as ToggleMenuOption).Toggled;
                            break;

                        case "SATClient.TextBoxMenuOption":
                            Console.SetCursorPosition(X + 6, Y + CurrentlySelected);
                            CurrentInputType = InputType.Text;
                            break;
                    }

                    break;
            }//[*] [X] [ ]
            Console.ForegroundColor = ConsoleColor.White;
        }

        static List<string> ServerNames = new List<string>()
        {
            "Server",
            "Hub",
            "Sat",
            "Grind",
            "All Servers"
        };
    }

    public class SelectMenuOption : MenuItem
    {

    }

    public class ToggleMenuOption : MenuItem
    {
        public Boolean Toggled = false;

    }

    public class TextBoxMenuOption : MenuItem
    {

    }

    public class Header : MenuItem
    {
       
    }

    public class MenuItem
    {
        public Boolean Enabled = true;
        public Boolean Selected = false;
        public string Text = "";
        public int MenuID = 0;
        public ConsoleColor Colour = ConsoleColor.White;
        public Dictionary<string, string> Information = new Dictionary<string, string>();
        public int IDInMenu = -1;
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

    #region Extensions
    static class Extensions
    {
        public static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable
        {
            return listToClone.Select(item => (T)item.Clone()).ToList();
        }
    }
    #endregion
}

#region Comments
/*
List<List<SelectMenuOption>> Menus = new List<List<SelectMenuOption>>()
        {
            new List<SelectMenuOption>() //Main Menu
            {
                new SelectMenuOption(){ MenuID = 1, Selected = true, Text = "Restart"},
                new SelectMenuOption(){ MenuID = 3, Selected = false, Text = "Troubleshooting"},
                new SelectMenuOption(){ MenuID = 2, Selected = false, Text = "Help"}
            },
            new List<SelectMenuOption>() //Restart Menu
            {
                new SelectMenuOption(){ MenuID = 4, Selected = true, Text = "Hub"},
                new SelectMenuOption(){ MenuID = 4, Selected = false, Text = "Sat"},
                new SelectMenuOption(){ MenuID = 4, Selected = false, Text = "Grind"}

            }
        };
*/
#endregion
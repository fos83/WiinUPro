﻿using Hardcodet.Wpf.TaskbarNotification;
using NintrollerLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Shared;
using Shared.Windows;

namespace WiinUSoft
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }

        private List<DeviceInfo> hidList;
        private List<DeviceControl> deviceList;

        public MainWindow()
        {
            //Nintroller.UseModestConfigs = true;
            hidList = new List<DeviceInfo>();
            deviceList = new List<DeviceControl>();

            InitializeComponent();

            Instance = this;

            //Test();
        }

        public void HideWindow()
        {
            if (WindowState == System.Windows.WindowState.Minimized)
            {
                trayIcon.Visibility = System.Windows.Visibility.Visible;
                Hide();
            }
        }

        public void ShowWindow()
        {
            trayIcon.Visibility = System.Windows.Visibility.Hidden;
            Show();
            WindowState = System.Windows.WindowState.Normal;
        }

        public void ShowBalloon(string title, string message, BalloonIcon icon)
        {
            ShowBalloon(title, message, icon, null);
        }

        public void ShowBalloon(string title, string message, BalloonIcon icon, SystemSound sound)
        {
            //if (trayIcon.Visibility == System.Windows.Visibility.Hidden)
            //    trayIcon.Visibility = System.Windows.Visibility.Visible;
            trayIcon.ShowBalloonTip(title, message, icon);

            if (sound != null)
            {
                sound.Play();
            }
        }

        private void Refresh()
        {
            hidList = WinBtStream.GetPaths();
            List<KeyValuePair<int, DeviceControl>> connectSeq = new List<KeyValuePair<int, DeviceControl>>();
            
            foreach (var hid in hidList)
            {
                DeviceControl existingDevice = null;

                foreach (DeviceControl d in deviceList)
                {
                    if (d.DevicePath == hid.DevicePath)
                    {
                        existingDevice = d;
                        break;
                    }
                }

                if (existingDevice != null)
                {
                    if (!existingDevice.Connected)
                    {
                        existingDevice.RefreshState();
                        if (existingDevice.properties.autoConnect && existingDevice.ConnectionState == DeviceState.Discovered)
                        {
                            connectSeq.Add(new KeyValuePair<int, DeviceControl>(existingDevice.properties.autoNum, existingDevice));
                        }
                    }
                }
                else
                {
                    var stream = new WinBtStream(hid.DevicePath);
                    Nintroller n = new Nintroller(stream, hid.Type);

                    if (stream.OpenConnection() && stream.CanRead)
                    {
                        deviceList.Add(new DeviceControl(n, hid.DevicePath));
                        deviceList[deviceList.Count - 1].OnConnectStateChange += DeviceControl_OnConnectStateChange;
                        deviceList[deviceList.Count - 1].RefreshState();
                        if (deviceList[deviceList.Count - 1].properties.autoConnect)
                        {
                            connectSeq.Add(new KeyValuePair<int, DeviceControl>(deviceList[deviceList.Count - 1].properties.autoNum, deviceList[deviceList.Count - 1]));
                        }
                    }
                }
            }

            // Auto connect in preferred order
            for (int i = 1; i < connectSeq.Count; i++)
            {
                if (connectSeq[i].Key < connectSeq[i - 1].Key)
                {
                    var tmp = connectSeq[i];
                    connectSeq[i] = connectSeq[i - 1];
                    connectSeq[i - 1] = tmp;
                    i = 0;
                }
            }

            int target = 0;
            while(!Holders.XInputHolder.availabe[target] && target < 4)
            {
                target++;
            }

            foreach(KeyValuePair<int, DeviceControl> d in connectSeq)
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<object>();
                new System.Threading.Timer(_ => tcs.SetResult(null)).Change(1000, -1);
                tcs.Task.Wait();

                if (Holders.XInputHolder.availabe[target] && target < 4 && d.Value.Device.Connect())
                {
                    d.Value.targetXDevice = target + 1;
                    d.Value.ConnectionState = DeviceState.Connected_XInput;
                    d.Value.Device.BeginReading();
                    d.Value.Device.GetStatus();
                    target++;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Version version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
                menu_version.Header = string.Format("version {0}.{1}.{2}", version.Major, version.Minor, version.Revision);
            }
            catch { }

            if (UserPrefs.Instance.startMinimized)
            {
                menu_StartMinimized.IsChecked = true;
                WindowState = System.Windows.WindowState.Minimized;
            }

            if (UserPrefs.Instance.autoStartup)
            {
                menu_AutoStart.IsChecked = true;
            }

            Refresh();
        }

        private void DeviceControl_OnConnectStateChange(DeviceControl sender, DeviceState oldState, DeviceState newState)
        {
            if (oldState == newState)
                return;

            switch (oldState)
            {
                case DeviceState.Discovered:
                    groupAvailable.Children.Remove(sender);
                    break;

                case DeviceState.Connected_XInput:
                    groupXinput.Children.Remove(sender);
                    break;

                case DeviceState.Connected_VJoy:
                    groupXinput.Children.Remove(sender);
                    break;
            }

            switch (newState)
            {
                case DeviceState.Discovered:
                    groupAvailable.Children.Add(sender);
                    break;

                case DeviceState.Connected_XInput:
                    groupXinput.Children.Add(sender);
                    break;

                case DeviceState.Connected_VJoy:
                    groupXinput.Children.Add(sender);
                    break;
            }
        }

        private void btnDetatchAllXInput_Click(object sender, RoutedEventArgs e)
        {
            List<DeviceControl> detatchList = new List<DeviceControl>();
            foreach (DeviceControl d in groupXinput.Children)
            {
                detatchList.Add(d);
            }
            foreach (DeviceControl d in detatchList)
            {
                d.Detatch();
            }
        }

        private void btnIdentify_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Window_StateChanged(object sender, System.EventArgs e)
        {
            HideWindow();
        }

        private void MenuItem_Show_Click(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void MenuItem_Refresh_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            foreach (DeviceControl dc in deviceList)
            {
                if (dc.ConnectionState == DeviceState.Connected_XInput
                 || dc.ConnectionState == DeviceState.Connected_VJoy)
                {
                    dc.Detatch();
                }
            }

            Close();
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (btnSettings.ContextMenu != null)
            {
                btnSettings.ContextMenu.IsOpen = true;
            }
        }

        private void menu_AutoStart_Click(object sender, RoutedEventArgs e)
        {
            menu_AutoStart.IsChecked = !menu_AutoStart.IsChecked;
            UserPrefs.AutoStart = menu_AutoStart.IsChecked;
            UserPrefs.SavePrefs();
        }

        private void menu_StartMinimized_Click(object sender, RoutedEventArgs e)
        {
            menu_StartMinimized.IsChecked = !menu_StartMinimized.IsChecked;
            UserPrefs.Instance.startMinimized = menu_StartMinimized.IsChecked;
            UserPrefs.SavePrefs();
        }

        private void menu_SetDefaultCalibration_Click(object sender, RoutedEventArgs e)
        {
            
        }

        #region Shortcut Creation
        public void CreateShortcut(string path)
        {
            IShellLink link = (IShellLink)new ShellLink();

            link.SetDescription("WiinUSoft");
            link.SetPath(new Uri(System.Reflection.Assembly.GetEntryAssembly().CodeBase).LocalPath);

            IPersistFile file = (IPersistFile)link;
            file.Save(Path.Combine(path, "WiinUSoft.lnk"), false);
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        internal interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
        #endregion

        public void Test()
        {
            Guid HumanInterfaceDeviceServiceClass_UUID = Guid.Parse("{0x00001124,0x0000,0x1000,{0x80,0x00,0x00,0x80,0x5F,0x9B,0x34,0xFB}}");

            IntPtr[] hRadios = new IntPtr[256];
            int nRadios;
            int nPaired = 0;

            // Enumerate BT radios
            IntPtr hFindRadio;
            NativeImports.Bluetooth_Find_Radio_Params radioParam;

            System.Diagnostics.Debug.WriteLine("Enumerating radios..");
            radioParam = new NativeImports.Bluetooth_Find_Radio_Params();
            radioParam.Initialize();

            nRadios = 0;
            hFindRadio = NativeImports.BluetoothFindFirstRadio(ref radioParam, out hRadios[nRadios++]);
            
            /*
            if (hFindRadio != IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Found Radio");
                while (NativeImports.BluetoothFindNextRadio(ref radioParam, out hRadios[nRadios++])) ;
                NativeImports.BluetoothFindRadioClose(ref hFindRadio);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Done Looking");
            }

            nRadios--;
            */
            System.Diagnostics.Debug.WriteLine(string.Format("Found {0} Radios", nRadios));

            if (hFindRadio != IntPtr.Zero)
            {
                while (nPaired == 0)
                {
                    int radio;

                    for (radio = 0; radio < nRadios; radio++)
                    {
                        NativeImports.Bluetooth_Radio_Info radioInfo;
                        IntPtr hFind;
                        NativeImports.BluetoothDeviceInfo btdi;
                        NativeImports.BLUETOOTH_DEVICE_SEARCH_PARAMS srch;

                        radioInfo = new NativeImports.Bluetooth_Radio_Info();
                        radioInfo.Initialize();
                        btdi = new NativeImports.BluetoothDeviceInfo();
                        btdi.Initialize();
                        srch = new NativeImports.BLUETOOTH_DEVICE_SEARCH_PARAMS();
                        srch.Initialize();

                        var err = NativeImports.BluetoothGetRadioInfo(hRadios[radio], ref radioInfo);
                        System.Diagnostics.Debug.WriteLine("Bluetooth Get Radio Info Exited with error code: " + err.ToString());

                        System.Diagnostics.Debug.WriteLine(string.Format("Radio {0}: {1} {2}", radio, radioInfo.szName, radioInfo.Address));

                        srch.fReturnAuthenticated = true;
                        srch.fReturnRemembered = true;
                        srch.fReturnConnected = true;
                        srch.fReturnUnknown = true;
                        srch.fIssueInquiry = true;
                        srch.cTimeoutMultiplier = 2;
                        srch.hRadio = hRadios[radio];

                        System.Diagnostics.Debug.WriteLine("Scanning");
                        
                        hFind = NativeImports.BluetoothFindFirstDevice(ref srch, ref btdi);

                        if (hFind == IntPtr.Zero)
                        {
                            System.Diagnostics.Debug.WriteLine("No Devices Found");
                        }
                        else
                        {
                            do
                            {
                                System.Diagnostics.Debug.WriteLine("Found: " + btdi.szName);
                                System.Diagnostics.Debug.WriteLine(radioInfo.Address);

                                if (btdi.szName.Contains("Nintendo RVL-CNT-01") || btdi.szName.Contains("Nintendo RVL-WBC-01"))
                                {
                                    StringBuilder pass = new StringBuilder();
                                    uint pcServices = 16;
                                    Guid[] guids = new Guid[16];
                                    bool error = false;

                                    if (!error)
                                    {
                                        if (btdi.fRemembered)
                                        {
                                            // Make Windows forget pairing
                                            var eCode = NativeImports.BluetoothRemoveDevice(ref btdi.Address);
                                            error = eCode == 0;
                                        }
                                    }

                                    // MAC address is passphrase
                                    var bytes = BitConverter.GetBytes(radioInfo.address);
                                    pass.Append((char)bytes[0]);
                                    pass.Append((char)bytes[1]);
                                    pass.Append((char)bytes[2]);
                                    pass.Append((char)bytes[3]);
                                    pass.Append((char)bytes[4]);
                                    pass.Append((char)bytes[5]);

                                    //System.Diagnostics.Debug.WriteLine("Password Copied to cliboard " + pass.ToString());
                                    //Clipboard.SetText(pass.ToString());

                                    if (!error)
                                    {
                                        // Pair
                                        var ePair = NativeImports.BluetoothAuthenticateDevice(IntPtr.Zero, hRadios[radio], ref btdi, pass.ToString(), 6);
                                        //var ePair = NativeImports.BluetoothAuthenticateDeviceEx(IntPtr.Zero, hRadios[radio], ref btdi, null, 0x00);
                                        error = ePair != 0;
                                        System.Diagnostics.Debug.WriteLine("Authed: " + error);
                                    }

                                    if (!error)
                                    {
                                        var eServ = NativeImports.BluetoothEnumerateInstalledServices(hRadios[radio], ref btdi, ref pcServices, guids);
                                        error = eServ != 0;
                                        System.Diagnostics.Debug.WriteLine("Services Installed: " + error);
                                    }

                                    if (!error)
                                    {
                                        // Activate Service
                                        var eAct = NativeImports.BluetoothSetServiceState(hRadios[radio], ref btdi, ref HumanInterfaceDeviceServiceClass_UUID, 0x01);
                                        error = eAct != 0;
                                        System.Diagnostics.Debug.WriteLine("Activated Service: " + error);
                                    }

                                    if (!error)
                                    {
                                        System.Diagnostics.Debug.WriteLine("Paired Device!");
                                        nPaired++;
                                    }
                                }

                            } while (NativeImports.BluetoothFindNextDevice(hFind, ref btdi));
                        }
                    }
                }

                // Clean up
                for (var r = 0; r < nRadios; r++)
                {
                    NativeImports.CloseHandle(hRadios[r]);
                }
            }
        }
    }

    class ShowWindowCommand : ICommand
    {
        public void Execute(object parameter)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.ShowWindow();
            }
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public event System.EventHandler CanExecuteChanged;
    }
}

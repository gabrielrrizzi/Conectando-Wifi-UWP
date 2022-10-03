// Copyright (c) Microsoft. All rights reserved.


using SDKTemplate;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WiFiConnect.Model;
using Windows.Devices.WiFi;
using Windows.Foundation.Metadata;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace WiFiConnect
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WiFiConnect_Scenario : Page
    {
        MainPage rootPage;
        private WiFiAdapter firstAdapter;
        List<string> passwords = new List<string>();
        bool sucess, connecting = false;
        int count = 0;
        WiFiNetworkDisplay selectedNetwork;
        WiFiReconnectionKind reconnectionKind;
        List<WifiConnect> wifiConnects = new List<WifiConnect>();

        public ObservableCollection<WiFiNetworkDisplay> ResultCollection
        {
            get;
            private set;
        }

        public WiFiConnect_Scenario()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            ResultCollection = new ObservableCollection<WiFiNetworkDisplay>();
            rootPage = MainPage.Current;

            // RequestAccessAsync must have been called at least once by the app before using the API
            // Calling it multiple times is fine but not necessary
            // RequestAccessAsync must be called from the UI thread
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed)
            {
                rootPage.NotifyUser("Access denied", NotifyType.ErrorMessage);
            }
            else
            {
                DataContext = this;

                var result = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
                if (result.Count >= 1)
                {
                    firstAdapter = await WiFiAdapter.FromIdAsync(result[0].Id);

                    /*var button = new Button();
                    button.Content = string.Format("Escanear Redes Wi-Fi");
                    button.Click += Button_Click;
                    Buttons.Children.Add(button);*/
                }
                else
                {
                    rootPage.NotifyUser("No WiFi Adapters detected on this machine.", NotifyType.ErrorMessage);
                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await firstAdapter.ScanAsync();
            }
            catch (Exception err)
            {
                rootPage.NotifyUser(String.Format("Erro ao escaner o Wifi: 0x{0:X}: {1}", err.HResult, err.Message), NotifyType.ErrorMessage);
                return;
            }

            DisplayNetworkReport(firstAdapter.NetworkReport);
        }

        public string GetCurrentWifiNetwork()
        {
            var connectionProfiles = NetworkInformation.GetConnectionProfiles();

            if (connectionProfiles.Count < 1)
            {
                return null;
            }

            var validProfiles = connectionProfiles.Where(profile =>
            {
                return (profile.IsWlanConnectionProfile && profile.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None);
            });

            if (validProfiles.Count() < 1)
            {
                return null;
            }

            ConnectionProfile firstProfile = validProfiles.First();

            return firstProfile.ProfileName;
        }

        private bool IsConnected(WiFiAvailableNetwork network)
        {
            if (network == null)
                return false;

            string profileName = GetCurrentWifiNetwork();
            if (!String.IsNullOrEmpty(network.Ssid) &&
                !String.IsNullOrEmpty(profileName) &&
                (network.Ssid == profileName))
            {
                return true;
            }

            return false;
        }

        private void DisplayNetworkReport(WiFiNetworkReport report)
        {
            rootPage.NotifyUser(string.Format("Network Report Timestamp: {0}", report.Timestamp), NotifyType.StatusMessage);

            ResultCollection.Clear();
            ConcurrentDictionary<string, WiFiNetworkDisplay> dictionary = new ConcurrentDictionary<string, WiFiNetworkDisplay>();

            foreach (var network in report.AvailableNetworks)
            {
                var item = new WiFiNetworkDisplay(network, firstAdapter);
                if (!String.IsNullOrEmpty(network.Ssid))
                {
                    dictionary.TryAdd(network.Ssid, item);
                }
                else
                {
                    string bssid = network.Bssid.Substring(0, network.Bssid.LastIndexOf(":"));
                    dictionary.TryAdd(bssid, item);
                }
            }

            var values = dictionary.Values;
            foreach (var item in values)
            {
                item.Update();
                if (IsConnected(item.AvailableNetwork))
                {
                    ResultCollection.Insert(0, item);
                    ResultsListView.SelectedItem = ResultsListView.Items[0];
                    ResultsListView.ScrollIntoView(ResultsListView.SelectedItem);
                    SwitchToItemState(item.AvailableNetwork, WifiConnectedState, false);
                }
                else
                {
                    ResultCollection.Add(item);
                }
            }
            ResultsListView.Focus(FocusState.Pointer);
        }

        private ListViewItem SwitchToItemState(object dataContext, DataTemplate template, bool forceUpdate)
        {
            if (forceUpdate)
            {
                ResultsListView.UpdateLayout();
            }
            var item = ResultsListView.ContainerFromItem(dataContext) as ListViewItem;
            if (item != null)
            {
                item.ContentTemplate = template;
            }
            return item;
        }

        private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedNetwork = ResultsListView.SelectedItem as WiFiNetworkDisplay;
            if (selectedNetwork == null)
            {
                return;
            }

            foreach (var item in e.RemovedItems)
            {
                SwitchToItemState(item, WifiInitialState, true);
            }

            foreach (var item in e.AddedItems)
            {
                var network = item as WiFiNetworkDisplay;
                SetSelectedItemState(network);
            }
        }

        private void SetSelectedItemState(WiFiNetworkDisplay network)
        {
            if (network == null)
                return;

            if (IsConnected(network.AvailableNetwork))
            {
                SwitchToItemState(network, WifiConnectedState, true);
            }
            else
            {
                SwitchToItemState(network, WifiConnectState, true);
            }
        }

        private void PushButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            DoWifiConnect(sender, e, true);
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            DoWifiConnect(sender, e, false);
        }

        private async void DoWifiConnect(object sender, RoutedEventArgs e, bool pushButtonConnect)
        {
            count = 0;
            sucess = false;
            var selectedNetwork = ResultsListView.SelectedItem as WiFiNetworkDisplay;
            if (selectedNetwork == null || firstAdapter == null)
            {
                rootPage.NotifyUser("Network not selected", NotifyType.ErrorMessage);
                return;
            }

            var ssid = selectedNetwork.AvailableNetwork.Ssid;
            if (string.IsNullOrEmpty(ssid))
            {
                if (string.IsNullOrEmpty(selectedNetwork.HiddenSsid))
                {
                    rootPage.NotifyUser("Ssid required for connection to hidden network.", NotifyType.ErrorMessage);
                    return;
                }
                else
                {
                    ssid = selectedNetwork.HiddenSsid;
                }
            }

            WiFiReconnectionKind reconnectionKind = WiFiReconnectionKind.Manual;
            if (selectedNetwork.ConnectAutomatically)
            {
                reconnectionKind = WiFiReconnectionKind.Automatic;
            }

            Task<WiFiConnectionResult> didConnect = null;

            if (pushButtonConnect)
            {
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5, 0))
                {
                    didConnect = firstAdapter.ConnectAsync(selectedNetwork.AvailableNetwork, reconnectionKind, null, string.Empty, WiFiConnectionMethod.WpsPushButton).AsTask();
                }
            }
            else
            {
                PasswordCredential credential = new PasswordCredential();  
                
                SwitchToItemState(selectedNetwork, WifiConnectingState, false);

                await ConnectWithPassword(selectedNetwork, reconnectionKind);               
            }
          

            // Since a connection attempt was made, update the connectivity level displayed for each
            foreach (var network in ResultCollection)
            {
                var task = network.UpdateConnectivityLevelAsync();
            }

        }

        private async Task ConnectWithPassword(WiFiNetworkDisplay _selectedNetwork, WiFiReconnectionKind _reconnectionKind)
        {
            selectedNetwork = _selectedNetwork;
            reconnectionKind = _reconnectionKind;

            await Task.Run(() => RealConnectPassword());
        }

        private async Task RealConnectPassword()
        {
            try
            {
                foreach (string pass in passwords)
                {
                    Task<WiFiConnectionResult> didConnect = null;
                    PasswordCredential credential = new PasswordCredential();
                    WifiConnect wifiConnect = null;                

                    var a = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        txtSenha.Text = "Senha: " + passwords[count];
                    });
                    credential.Password = pass;
                    var b = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        txtTesteSenha.Text = Convert.ToString(++count) + " de " + passwords.Count;
                    });

                    if (selectedNetwork.IsHiddenNetwork)
                    {
                        // Hidden networks require the SSID to be supplied
                        didConnect = firstAdapter.ConnectAsync(selectedNetwork.AvailableNetwork, reconnectionKind, credential, selectedNetwork.AvailableNetwork.Ssid).AsTask();
                    }
                    else
                    {
                        didConnect = firstAdapter.ConnectAsync(selectedNetwork.AvailableNetwork, reconnectionKind, credential).AsTask();
                    }
                    connecting = true;
                    if (didConnect != null)
                    {
                        wifiConnect = new WifiConnect();
                        wifiConnect.ConnectionResult = await didConnect;

                        wifiConnect.Password = credential;

                        wifiConnects.Add(wifiConnect);
                        connecting = false;
                    }

                    if (wifiConnect != null && wifiConnect.ConnectionResult.ConnectionStatus == WiFiConnectionStatus.Success) { break; }
                }
                foundConnectSucess();

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        //private bool checkWifiConnect(WiFiConnectionResult result, WiFiNetworkDisplay selectedNetwork, PasswordCredential credential)
        //{
        //    if (!sucess)
        //    {
        //        if (result != null && result.ConnectionStatus == WiFiConnectionStatus.Success)
        //        {
        //            var b = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        //            {
        //                rootPage.NotifyUser(string.Format("Successfully connected to {0}.", selectedNetwork.Ssid), NotifyType.StatusMessage);
        //            });
        //            // refresh the webpage
        //            /*webViewGrid.Visibility = Visibility.Visible;
        //            toggleBrowserButton.Content = "Hide Browser Control";
        //            refreshBrowserButton.Visibility = Visibility.Visible;*/
        //            var a = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        //            {
        //                ResultCollection.Remove(selectedNetwork);
        //                ResultCollection.Insert(0, selectedNetwork);
        //                ResultsListView.SelectedItem = ResultsListView.Items[0];
        //                ResultsListView.ScrollIntoView(ResultsListView.SelectedItem);

        //                SwitchToItemState(selectedNetwork, WifiConnectedState, false);
        //                foreach (var network in ResultCollection)
        //                {
        //                    var task = network.UpdateConnectivityLevelAsync();
        //                }
        //                txtSenha.Text = "Senha: " + credential.Password;
        //            });
        //            return true;
        //        }
        //        else
        //        {
        //            if (result.ConnectionStatus == WiFiConnectionStatus.Timeout)
        //            {
        //                firstAdapter.Disconnect();
        //            }
        //            var a = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        //            {
        //                rootPage.NotifyUser(string.Format("Não foi possível se conectar à {0}. Error: {1}", selectedNetwork.Ssid, (result != null ? result.ConnectionStatus : WiFiConnectionStatus.UnspecifiedFailure)), NotifyType.ErrorMessage);
        //                SwitchToItemState(selectedNetwork, WifiConnectState, false);
        //            });

        //            //rootPage.NotifyUser(string.Format("Não foi possível se conectar à {0}. Error: {1}", selectedNetwork.Ssid, (result != null ? result.ConnectionStatus : WiFiConnectionStatus.UnspecifiedFailure)), NotifyType.ErrorMessage);
        //            //SwitchToItemState(selectedNetwork, WifiConnectState, false);
        //            return false;
        //        }
        //    }
        //    return false;
        //}

        //private PasswordCredential FoundPassword(WiFiNetworkDisplay selectedNetwork)
        //{
        //    PasswordCredential credential = new PasswordCredential();
        //    if (selectedNetwork.IsEapAvailable && selectedNetwork.UsePassword)
        //    {
        //        if (!String.IsNullOrEmpty(selectedNetwork.Domain))
        //        {
        //            credential.Resource = selectedNetwork.Domain;
        //        }

        //        credential.UserName = selectedNetwork.UserName ?? "";
        //        credential.Password = selectedNetwork.Password ?? "";
        //    }
        //    else
        //    {
        //        var a = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        //        {
        //            txtSenha.Text = "Senha: " + passwords[count];
        //        });
        //        //txtSenha.Text = "Senha: " + passwords[count];
        //        credential.Password = passwords[count];
        //        var b = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        //        {
        //            txtTesteSenha.Text = Convert.ToString(count + 1) + " de " + passwords.Count;
        //        });

        //        //txtTesteSenha.Text = Convert.ToString(count +  1) + " de " + passwords.Count;
        //    }

        //    return credential;
        //}

        //private bool exceptLimit()
        //{
        //    if ((count == passwords.Count) || (sucess == true))
        //    {
        //        var b = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        //        {
        //            foundConnectSucess();
        //        });
        //        return true;
        //    }
        //    return false;
        //}

        private void foundConnectSucess()
        {
            WifiConnect wifi = wifiConnects.FirstOrDefault(x => x.ConnectionResult.ConnectionStatus == WiFiConnectionStatus.Success);
            var a = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (wifi != null)
                {
                    rootPage.NotifyUser(string.Format("Successfully connected to {0}.", selectedNetwork.Ssid), NotifyType.StatusMessage);
                    ResultCollection.Remove(selectedNetwork);
                    ResultCollection.Insert(0, selectedNetwork);
                    ResultsListView.SelectedItem = ResultsListView.Items[0];
                    ResultsListView.ScrollIntoView(ResultsListView.SelectedItem);

                    SwitchToItemState(selectedNetwork, WifiConnectedState, false);
                    foreach (var network in ResultCollection)
                    {
                        var task = network.UpdateConnectivityLevelAsync();
                    }
                    txtSenha.Text = "Senha: " + wifi.Password.Password;
                }
                else
                {
                    firstAdapter.Disconnect();
                    rootPage.NotifyUser(string.Format("Não foi possível se conectar à {0}. Error: {1}", selectedNetwork.Ssid, (wifi != null ? wifi.ConnectionResult.ConnectionStatus : WiFiConnectionStatus.UnspecifiedFailure)), NotifyType.ErrorMessage);
                    SwitchToItemState(selectedNetwork, WifiConnectState, false);
                };
            });
        }

        private void Browser_Toggle_Click(object sender, RoutedEventArgs e)
        {
           /* if (webViewGrid.Visibility == Visibility.Visible)
            {
                webViewGrid.Visibility = Visibility.Collapsed;
                refreshBrowserButton.Visibility = Visibility.Collapsed;
                toggleBrowserButton.Content = "Show Browser Control";
            }
            else
            {
                webViewGrid.Visibility = Visibility.Visible;
                refreshBrowserButton.Visibility = Visibility.Visible;
                toggleBrowserButton.Content = "Hide Browser Control";
            }*/
        }
        private void Browser_Refresh(object sender, RoutedEventArgs e)
        {
           // webView.Refresh();
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            var selectedNetwork = ResultsListView.SelectedItem as WiFiNetworkDisplay;
            if (selectedNetwork == null || firstAdapter == null)
            {
                rootPage.NotifyUser("Network not selected", NotifyType.ErrorMessage);
                return;
            }

            selectedNetwork.Disconnect();
            SetSelectedItemState(selectedNetwork);
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            txtArquivo.Visibility = Visibility.Visible;
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".txt");

            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                this.txtArquivo.Text = "Arquivo: " + file.Name;

                string text = await Windows.Storage.FileIO.ReadTextAsync(file);

                passwords = text.Replace("\r", "").Split('\n').ToList();
                passwords.RemoveAll(x => String.IsNullOrEmpty(x));


                this.txtTesteSenha.Text = "0 de " + passwords.Count.ToString();
            }
            else
            {
                this.txtArquivo.Text = "Não conseguimos abrir o arquivo.";
            }
        }
    }
}


﻿public partial class MainWindow : Window
{
    public static IFConnectorClient client = new IFConnectorClient();
    public static Commands commands = new Commands();



    private bool connectionStatus;

    public MainWindow()
    {
        InitializeComponent();




    }

    void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
    {
        airbrake.NotifyAsync(e.ExceptionObject as System.Exception);
        MessageBox.Show(e.ExceptionObject.ToString());
    }


    /*
        Connections to API, reading in values, etc.
        ===========================
    */


    void receiver_DataReceived(object sender, EventArgs e)
    {
        byte[] data = (byte[])sender;

        var apiServerInfo = Serializer.DeserializeJson<APIServerInfoLegacy>(UTF8Encoding.UTF8.GetString(data));

        if (apiServerInfo != null)
        {
            Console.WriteLine("Received Server Info from: {0}:{1}", apiServerInfo.Address, apiServerInfo.Port);
            receiver.Stop();
            if (apiServerInfo.Address != null)
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    // Legacy version
                    MessageBox.Show("The version of Infinite Flight you are trying to connect to is no longer supported. Please update Infinite Flight in the App Store or the Google Play Store to the latest version.", "There was a problem");
                    Application.Current.Shutdown();
                }));
            }
            else
            {
                // Use new method
                DataReceivedNewMethod(data);
            }
        }
        else
        {
            Console.WriteLine("Invalid Server Info Received with old method");
            DataReceivedNewMethod(data);
        }
    }

    private void DataReceivedNewMethod(byte[] data)
    {
        Console.WriteLine("Attempting to connect with new method...");
        var apiServerInfo = Serializer.DeserializeJson<APIServerInfo>(UTF8Encoding.UTF8.GetString(data));

        if (apiServerInfo != null)
        {
            Console.WriteLine("Received Server Info from: {0}:{1}", apiServerInfo.Addresses.ToString(), apiServerInfo.Port);
            receiver.Stop();
            Dispatcher.BeginInvoke((Action)(() =>
            {
                var ipToConnectTo = apiServerInfo.Addresses[0];
                for (var i = 0; i < apiServerInfo.Addresses.Length; i++)
                {
                    // Prefer IPv4 if available
                    Match match = Regex.Match(apiServerInfo.Addresses[i], @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
                    if (match.Success)
                    {
                        ipToConnectTo = match.Value;
                    }
                }
                Connect(IPAddress.Parse(ipToConnectTo), apiServerInfo.Port);
            }));
        }
        else
        {
            Console.WriteLine("Invalid Server Info Received");
        }
    }

    private void Connect(IPAddress iPAddress, int port)
    {
        client.Connect(iPAddress.ToString(), port);
        FMSControl.Client = client;

        // set connected bool
        connectionStatus = true;

        // set label text
        ipLabel.Content = String.Format("Infinite Flight is at {0}", iPAddress.ToString());

        overlayGrid.Visibility = System.Windows.Visibility.Collapsed;
        mainTabControl.Visibility = System.Windows.Visibility.Visible;

        client.CommandReceived += client_CommandReceived;
        client.Disconnected += client_Disconnected;

        client.SendCommand(new APICall { Command = "InfiniteFlight.GetStatus" });
        client.SendCommand(new APICall { Command = "Live.EnableATCMessageListUpdated" });

        Task.Run(() =>
        {

            while (connectionStatus == true)
            {
                try
                {
                    client.SendCommand(new APICall { Command = "Airplane.GetState" });
                    Thread.Sleep(200);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception whilst getting aircraft state: {0}", ex);
                }
            }
        });

        Task.Run(() =>
        {

            while (connectionStatus == true)
            {
                try
                {
                    client.SendCommand(new APICall { Command = "Live.GetTraffic" });
                    client.SendCommand(new APICall { Command = "Live.ATCFacilities" });

                    Thread.Sleep(5000);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception whilst getting Live state: {0}", ex);
                }
            }
        });
    }

    private void client_Disconnected(object sender, CommandReceivedEventArgs e)
    {
        Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { connectionLost(); });
    }

    private void connectionLost()
    {
        if (connectionStatus)
        {
            connectionStatus = false;
            overlayGrid.Visibility = System.Windows.Visibility.Visible;
            mainTabControl.Visibility = System.Windows.Visibility.Collapsed;
            client = new IFConnectorClient();
            receiver.Stop();
            receiver.StartListening();
        }
    }

    void client_CommandReceived(object sender, CommandReceivedEventArgs e)
    {
        Dispatcher.BeginInvoke((Action)(() =>
        {
            try
            {
                // System.Diagnostics.Debug.WriteLine(e.CommandString);
                var type = typeof(IFAPIStatus).Assembly.GetType(e.Response.Type);

                if (type == typeof(APIAircraftState))
                {
                    var state = Serializer.DeserializeJson<APIAircraftState>(e.CommandString);

                    // convert to fpm
                    state.VerticalSpeed = float.Parse(Convert.ToString(state.VerticalSpeed * 200, CultureInfo.InvariantCulture.NumberFormat), CultureInfo.InvariantCulture.NumberFormat); // multiply by 200, this somehow gets it accurate..

                    airplaneStateGrid.DataContext = null;
                    airplaneStateGrid.DataContext = state;
                    pAircraftState = state;
                    if (FMSControl.autoFplDirectActive) { FMSControl.updateAutoNav(state); }
                    if (FMSControl.HoldingActive) { FMSControl.performHold(state); }
                    AircraftStateControl.AircraftState = state;
                    AttitudeIndicator.updateAttitude(state.Pitch, state.Bank);
                    updateLandingRoll(state);
                }
                else if (type == typeof(GetValueResponse))
                {
                    var state = Serializer.DeserializeJson<GetValueResponse>(e.CommandString);

                    Console.WriteLine("{0} -> {1}", state.Parameters[0].Name, state.Parameters[0].Value);
                }
                else if (type == typeof(LiveAirplaneList))
                {
                    LiveAirplaneList airplaneList = Serializer.DeserializeJson<LiveAirplaneList>(e.CommandString);
                    //airplaneDataGrid.ItemsSource = airplaneList.Airplanes;
                }
                else if (type == typeof(FacilityList))
                {
                    var facilityList = Serializer.DeserializeJson<FacilityList>(e.CommandString);

                    //facilitiesDataGrid.ItemsSource = facilityList.Facilities;
                }
                else if (type == typeof(IFAPIStatus))
                {
                    var status = Serializer.DeserializeJson<IFAPIStatus>(e.CommandString);

                }
                else if (type == typeof(APIATCMessage))
                {
                    var msg = Serializer.DeserializeJson<APIATCMessage>(e.CommandString);

                    //Handle the ATC message to control the autopilot if enabled by checkbox
                    FMSControl.handleAtcMessage(msg, pAircraftState);

                    // TODO client.ExecuteCommand("Live.GetCurrentCOMFrequencies");
                }
                else if (type == typeof(APIFrequencyInfoList))
                {
                    var msg = Serializer.DeserializeJson<APIFrequencyInfoList>(e.CommandString);
                }
                else if (type == typeof(ATCMessageList))
                {
                    var msg = Serializer.DeserializeJson<ATCMessageList>(e.CommandString);
                    atcMessagesDataGrid.ItemsSource = msg.ATCMessages;

                }
                else if (type == typeof(APIFlightPlan))
                {
                    var msg = Serializer.DeserializeJson<APIFlightPlan>(e.CommandString);
                    Console.WriteLine("Flight Plan: {0} items", msg.Waypoints.Length);
                    FMSControl.fplReceived(msg); //Update FMS with FPL from IF.
                    foreach (var item in msg.Waypoints)
                    {
                        Console.WriteLine(" -> {0} {1} - {2}, {3}", item.Name, item.Code, item.Latitude, item.Longitude);
                    }
                }
                else if (type == typeof(APIAutopilotState))
                {
                    FMSControl.APState = Serializer.DeserializeJson<APIAutopilotState>(e.CommandString);
                }
            }
            catch (System.NullReferenceException)
            {
                Console.WriteLine("Disconnected from server!");
                //Let the client handle the lost connection.
                //connectionStatus = false;
            }
        }));
    }

    private APIAircraftState pLastState, pStateJustBeforeTouchdown, pStateJustAfterTouchdown;
    private Coordinate pLandingLocation;
    private double pLastLandingRoll = 0.0;
    private LandingStats pLandingStatDlg;
    private void updateLandingRoll(APIAircraftState state)
    {
        if (pLastState == null)
        {
            pLastState = state;
        }
        else if (!state.IsOnGround)
        {
            pLastState = state;
            pLastLandingRoll = 0.0;
            pLandingLocation = null;
            txtLandingRoll.Visibility = Visibility.Hidden;
            txtLandingRollLabel.Visibility = Visibility.Hidden;
            //btnViewLandingStats.Visibility = Visibility.Hidden;
        }
        else if (!pLastState.IsLanded && state.IsLanded) //Just transitioned to "landed" state, so start the roll accumulation
        {
            pLandingLocation = state.Location;
            pStateJustBeforeTouchdown = pLastState;
            pStateJustAfterTouchdown = state;
            pLastState = state;
        }
        else if (state.IsLanded && pLandingLocation != null) //We are in landed state. Calc the roll length.
        {
            Coordinate currentPosition = state.Location;
            var R = 3959; // Radius of the earth in miles
            var dLat = (currentPosition.Latitude - pLandingLocation.Latitude) * (Math.PI / 180);
            var dLon = (currentPosition.Longitude - pLandingLocation.Longitude) * (Math.PI / 180);
            var a =
              Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
              Math.Cos((currentPosition.Latitude) * (Math.PI / 180)) * Math.Cos((pLandingLocation.Latitude) * (Math.PI / 180)) *
              Math.Sin(dLon / 2) * Math.Sin(dLon / 2)
              ;
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; // Distance in miles
            d *= 5280; //Distance in ft
            txtLandingRoll.Text = String.Format("{0:0.00}", d) + " ft";
            txtLandingRoll.Visibility = Visibility.Visible;
            txtLandingRollLabel.Visibility = Visibility.Visible;
            pLastState = state;
            landingDetails.updateLandingStats(pLandingLocation, pAircraftState.Location, pStateJustBeforeTouchdown, pStateJustAfterTouchdown, "");
        }

        if (pLandingLocation != null && state.IsLanded && state.IsOnGround && state.GroundSpeedKts < 10)
        {
            pLandingStatDlg = new LandingStats();
            pLandingStatDlg.updateLandingStats(pLandingLocation, pAircraftState.Location, pStateJustBeforeTouchdown, pStateJustAfterTouchdown, "");
            landingDetails.updateLandingStats(pLandingLocation, pAircraftState.Location, pStateJustBeforeTouchdown, pStateJustAfterTouchdown, "");
            //btnViewLandingStats.Visibility = Visibility.Visible;
        }

    }

    private void btnViewLandingStats_Click(object sender, RoutedEventArgs e)
    {
        Window window = new Window
        {
            Title = "Last Landing Stats",
            Content = pLandingStatDlg,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize
        };

        window.ShowDialog();

    }

    #endregion

    private void tabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabItem_ATC.IsSelected)
        {
            commands.atcMenu();
        }

    }

    private void enableATCMessagesButton_Click(object sender, RoutedEventArgs e)
    {
        client.ExecuteCommand("Live.EnableATCMessageNotification");
    }


    private void atcMessagesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var command = string.Format("Commands.ATCEntry{0}", atcMessagesDataGrid.SelectedIndex + 1);

        client.ExecuteCommand(command);
    }



    #region Keyboard commands
    /*
        Keyboard Commands
        ===========================
    */

    private void keyDownEvent(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // check if a field is focused
        if (KeyboardCommandHandler.keyboardCommandsDisabled != FlightPlanDatabase.FlightPlanDb.textFieldFocused)
        {
            KeyboardCommandHandler.keyboardCommandsDisabled = FlightPlanDatabase.FlightPlanDb.textFieldFocused;
        }

        if (KeyboardCommandHandler.keyboardCommandsDisabled != IF_FMS.FMS.textFieldFocused)
        {
            KeyboardCommandHandler.keyboardCommandsDisabled = IF_FMS.FMS.textFieldFocused;
        }

        Console.WriteLine("Key pressed: {0}", e.Key);

        KeyboardCommandHandler.keyPressed(e.Key);

    }

    private void keyUpEvent(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Console.WriteLine("KeyUp: {0}", e.Key);

        KeyboardCommandHandler.keyUp(e.Key);
    }

    #endregion

    #region Menu items
    /*
        Menu Items
        ===========================
    */

    // Camera menu

    private void nextCameraMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.nextCamera();
    }

    private void previousCameraMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.previousCamera();
    }

    private void cockpitCameraMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.cockpitCamera();
    }

    private void virtualCockpitCameraMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.vcCamera();
    }

    private void followCameraMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.followCamera();
    }

    private void onBoardCameraMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.onboardCamera();
    }

    private void fybyCameraMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.flybyCamera();
    }

    private void towerCameraMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.towerCamera();
    }

    //  Controls menu

    private void landingGearMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.landingGear();
    }

    private void spoilersMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.spoilers();
    }

    private void flapsUpMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.flapsUp();
    }

    private void flapsDownMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.flapsDown();
    }

    private void parkingBrakesMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.parkingBrake();
    }

    private void autopilotMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.autopilot();
    }

    private void pushbackMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.pushback();
    }

    private void pauseMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.pause();
    }

    //  Lights menu

    private void landingLightsMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.landing();
    }

    private void strobeLightsMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.strobe();
    }

    private void navLightsMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.nav();
    }

    private void beaconLightsMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.beacon();
    }

    //  Live menu

    private void atcWindowMenu_Click(object sender, RoutedEventArgs e)
    {
        commands.atcMenu();
    }

    //  Help menu

    private void joystickSetupGuide(object sender, RoutedEventArgs e)
    {

        // go to liveflight help site
        var URL = "http://help.liveflightapp.com";
        System.Diagnostics.Process.Start(URL);

    }

    private void sourceCodeMenu_Click(object sender, RoutedEventArgs e)
    {

        // go to GitHub
        var URL = "https://github.com/LiveFlightApp/Connect-Windows";
        System.Diagnostics.Process.Start(URL);

    }

    private void communityMenu_Click(object sender, RoutedEventArgs e)
    {
        // go to Community
        var URL = "http://community.infinite-flight.com/?u=carmalonso";
        System.Diagnostics.Process.Start(URL);
    }

    private void liveFlightMenu_Click(object sender, RoutedEventArgs e)
    {
        // go to LiveFlight
        var URL = "http://www.liveflightapp.com";
        System.Diagnostics.Process.Start(URL);
    }

    private void lfFacebookMenu_Click(object sender, RoutedEventArgs e)
    {
        // go to LiveFlight Facebook
        var URL = "http://facebook.com/LiveFlightApp/";
        System.Diagnostics.Process.Start(URL);
    }

    private void lfTwitterMenu_Click(object sender, RoutedEventArgs e)
    {
        // go to LiveFlight Twitter
        var URL = "http://twitter.com/LiveFlightApp/";
        System.Diagnostics.Process.Start(URL);
    }

    private void aboutLfMenu_Click(object sender, RoutedEventArgs e)
    {
        AboutWindow about = new AboutWindow();
        about.Show();
    }

    #endregion

    #region "FlightPlanDatabase"
    /*
        FPD work
        ===========================
    */

    private void FlightPlanDb_FplUpdated(object sender, EventArgs e)
    {
        FMSControl.FPLState = new IF_FMS.FMS.flightPlanState(); //Clear state of FMS
        FMSControl.CustomFPL.waypoints.Clear(); //Clear FPL

        foreach (IF_FMS.FMS.fplDetails f in FpdControl.FmsFpl)
        { //Load waypoints to FMS
            FMSControl.CustomFPL.waypoints.Add(f);
        }
        FMSControl.FPLState.fpl = FpdControl.ApiFpl;
        FMSControl.FPLState.fplDetails = FMSControl.CustomFPL;

        //Go to FMS tab so user can see flight plan
        mainTabControl.SelectedIndex = mainTabControl.SelectedIndex - 1;
    }





    #endregion

    private void expander_Expanded(object sender, RoutedEventArgs e)
    {
        this.Width = 1125;
        expander.Header = "Collapse";
    }

    private void expander_Collapsed(object sender, RoutedEventArgs e)
    {
        this.Width = 525;
        expander.Header = "Expand";
    }
}
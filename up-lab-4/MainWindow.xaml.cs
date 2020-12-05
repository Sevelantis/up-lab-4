using BruTile.Predefined;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projection;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace up_lab_4
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string portName = null;
        private SerialPort port = null;
        private Queue data = null;
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private Boolean isTracking = false;
        private Map map = null;
        private Dictionary<string, string> parsed = null;
        public MainWindow()
        {
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            PortComboBox.ItemsSource = SerialPort.GetPortNames();

            worker.WorkerSupportsCancellation = true;
            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            //MyMapControl.Map.Layers.Add(new TileLayer(KnownTileSources.Create()));

            map = new Map();
            map.Layers.Add(OpenStreetMap.CreateTileLayer());

            // Get the lon lat coordinates from somewhere (Mapsui can not help you there)
            var centerOfLondonOntario = new Point(-81.2497, 42.9837);
            // OSM uses spherical mercator coordinates. So transform the lon lat coordinates to spherical mercator
            var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(centerOfLondonOntario.X, centerOfLondonOntario.Y);
         
            map.Home = n => n.NavigateTo(sphericalMercatorCoordinate, map.Resolutions[9]);

            MyMapControl.Map = map;
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (isTracking)
            {
                Array lines = data.ToArray();
                parsed = ParseNmea(lines);

                ReceivedTextBox.Text = "";
                //foreach (KeyValuePair<string, string> entry in parsed)
                //{
                //    ReceivedTextBox.AppendText(entry.Key + ": " + entry.Value + "\n");
                //    ReceivedTextBox.ScrollToEnd();
                //}
                ReceivedTextBox.AppendText("time: " + parsed["time"]);
                ReceivedTextBox.AppendText("\nlongitude: " + parsed["lon_readable"]);
                ReceivedTextBox.AppendText("\nlatitude: " + parsed["lat_readable"]);
                ReceivedTextBox.AppendText("\nsatellites: " + parsed["sats"]);


                map = new Map();
                map.Layers.Add(OpenStreetMap.CreateTileLayer());
                var center = CalcPosition();
                var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(center.X, center.Y);
                map.Home = n => n.NavigateTo(sphericalMercatorCoordinate, map.Resolutions[9]);
                MyMapControl.Map = map;

                worker.RunWorkerAsync();
            }
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if(!worker.CancellationPending)
            {
                Thread.Sleep(2000);
            }
        }

        private Point CalcPosition()
        {
            double lat = Double.Parse(parsed["lat"], System.Globalization.CultureInfo.InvariantCulture);
            double lon = Double.Parse(parsed["lon"], System.Globalization.CultureInfo.InvariantCulture);

            int lat_d = 1;
            int lon_d = 1;
            if (parsed["lat_d"] == "S") lat_d = -1;
            if (parsed["lon_d"] == "W") lon_d = -1;

            return new Point((double)(lon_d * lon/100), (double)(lat_d * lat/100));

        }

        private void PortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            portName = (string)PortComboBox.SelectedItem;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if(portName == null)
            {
                MessageBox.Show("You didn't choose a port");
            }
            else
            {
                if(port != null)
                {
                    port.Close();
                }
                port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
                port.Handshake = Handshake.XOnXOff;
                port.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
                port.Open();
                data = new Queue(10);
                ReceivedTextBox.Text = "Connected to " + portName;
            }

        }
        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort port = (SerialPort)sender;
            while(port.BytesToRead > 0)
            {
                data.Enqueue(port.ReadLine());
            }
            
        }

        private Dictionary<string, string> ParseNmea(Array lines)
        {
            Dictionary<string, string> parsed = new Dictionary<string, string>();
            parsed["sats"] = "N/A";
            parsed["time"] = "N/A";
            parsed["lat_readable"] = "N/A";
            parsed["lon_readable"] = "N/A";
            parsed["lat"] = "51";
            parsed["lat_d"] = "N";
            parsed["lon"] = "17";
            parsed["lon_d"] = "E";
            foreach (string line in lines)
            {
                if(line.Contains("GPGLL"))
                {
                    parsed["llorg"] = line;
                    string[] split = line.Split('*');
                    split = split[0].Split(',');
                    parsed["lat"] = split[1];
                    parsed["lat_d"] = split[2];
                    parsed["lon"] = split[3];
                    parsed["lon_d"] = split[4];

                    parsed["lat_readable"] = parsed["lat"].Substring(0, 2) + "d " + parsed["lat"].Substring(2, 7) + "' " + parsed["lat_d"];
                    parsed["lon_readable"] = Int32.Parse(parsed["lon"].Substring(0, 3)).ToString() + "d " + parsed["lon"].Substring(3, 7) + "' " + parsed["lon_d"];
                }
                else if(line.Contains("GPGSV"))
                {
                    parsed["satorg"] = line;
                    string[] split = line.Split(',');
                    parsed["sats"] = Int32.Parse(split[3]).ToString();
                }
                else if(line.Contains("GPGGA"))
                {
                    parsed["gga"] = line;
                    string[] split = line.Split(',');
                    var time = TimeSpan.Parse(split[1].Substring(0, 2) + ":" + split[1].Substring(2, 2) + ":" + split[1].Substring(4, 2));
                    var dateTime = DateTime.Today.Add(time);
                    dateTime = dateTime.AddHours(1);
                    parsed["time"] = dateTime.ToString();
                }
            }
            return parsed;
        }

        private void TrackButton_Click(object sender, RoutedEventArgs e)
        {
            if(isTracking)
            {
                isTracking = false;
                TrackButton.Content = "Start Tracking";
                ReceivedTextBox.Text = "Connected to " + portName;
            }
            else
            {
                isTracking = true;
                TrackButton.Content = "Stop Tracking";
                worker.CancelAsync();
                while (worker.IsBusy)
                {
                    Thread.Sleep(10);
                }
                worker.RunWorkerAsync();
            }  
        }

    }
}

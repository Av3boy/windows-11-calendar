using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

using Microsoft.Identity.Client;
using Microsoft.Graph;
using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Win32;
using System.Globalization;
using System.Windows.Interop;

using Microsoft.Extensions.Configuration;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool startMenuOpen { get; set; }

        // We intend to obtain a token for Graph for the following scopes (permissions)
        string[] scopes = new[] { "user.read", "calendars.read", "Calendars.ReadWrite", "email" };

        private PublicClientApplicationOptions appConfiguration = null;
        private IConfiguration configuration;
        private string MSGraphURL;
        private GraphServiceClient graphClient;

        // The MSAL Public client app
        private IPublicClientApplication application;

        public MainWindow()
        {
            InitializeComponent();

            this.WindowStartupLocation = WindowStartupLocation.Manual;

            var height = System.Windows.SystemParameters.PrimaryScreenWidth;
            var width = System.Windows.SystemParameters.PrimaryScreenHeight;

            this.Left = width + 250;
            this.Top = 500;

        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            // Using appsettings.json for our configuration settings
            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            configuration = builder.Build();

            appConfiguration = configuration
                .Get<PublicClientApplicationOptions>();

            MSGraphURL = configuration.GetValue<string>("GraphApiUrl");

            // Sign-in user using MSAL and obtain an access token for MS Graph
            graphClient = await SignInAndInitializeGraphServiceClient(appConfiguration, scopes);

            // start timer
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += MyTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        private async Task<string> SignInUserAndGetTokenUsingMSAL(PublicClientApplicationOptions configuration, string[] scopes)
        {
            string authority = string.Concat(configuration.Instance, configuration.TenantId);

            // Initialize the MSAL library by building a public client application
            application = PublicClientApplicationBuilder.Create(configuration.ClientId)
                                                    .WithAuthority(authority)
                                                    .WithDefaultRedirectUri()
                                                    .Build();


            AuthenticationResult result;
            try
            {
                var accounts = await application.GetAccountsAsync();
                result = await application.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                 .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                result = await application.AcquireTokenInteractive(scopes)
                 .WithClaims(ex.Claims)
                 .ExecuteAsync();
            }

            return result.AccessToken;
        }

        /// <summary>
        /// Sign in user using MSAL and obtain a token for MS Graph
        /// </summary>
        /// <returns></returns>
        private async Task<GraphServiceClient> SignInAndInitializeGraphServiceClient(PublicClientApplicationOptions configuration, string[] scopes)
        {
            GraphServiceClient graphClient = new GraphServiceClient(MSGraphURL,
                new DelegateAuthenticationProvider(async (requestMessage) =>
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", await SignInUserAndGetTokenUsingMSAL(configuration, scopes));
                }));

            return await Task.FromResult(graphClient);
        }

        private void MyTimer_Tick(object sender, EventArgs e)
        {
            RegistryKey hklm = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);

            if (hklm != null)
            {
                RegistryKey key = hklm.OpenSubKey(@"Software\StartMenuDetection");

                if (key != null)
                {
                    var open = key.GetValue("Open");

                    // TODO:
                    // if (open != null && (int)open == 1 && startMenuOpen == false)
                    //     this.Show();
                    // else
                    //     Hide();
                }

                key.Close();
            }

            hklm.Close();

        }

        private async void calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            DateTime? date = calendar.SelectedDate;

            // Call the /me endpoint of MS Graph
            DateTime end = date.Value.AddDays(1);

            // Convert date time to correct format
            string startDate = date.Value.ToString("yyyy/MM/dd");
            string endDate = end.ToString("yyyy/MM/dd");

            string newStart = startDate.Replace("/", "-");
            string newEnd = endDate.Replace("/", "-");

            // Construct the date time query parameters for the selected date
            var queryOptions = new List<QueryOption>()
            {
                new QueryOption("startdatetime", newStart),// "2021-11-20T21:14:40.477Z"),
                new QueryOption("enddatetime", newEnd)//"2021-11-27T21:14:40.477Z")
            };

            // Get event data
            var events = await graphClient.Me.Calendar.CalendarView
                .Request(queryOptions)
                .GetAsync();

            // Apply to the list
            for (int i = 0; i < events.Count; i++)
            {
                list.Items.Add($"{events[i].Subject}: {DateTime.Parse(events[i].Start.DateTime).ToString("HH:mm")}");
            }
        }
    }

    public static class Extensions
    {
        /// <summary>
        /// Page fade in milliseconds
        /// </summary>
        private static readonly int Time = 2; 

        public static async void Show(this Page page)
        {
            for (int i = 99; i >= 0; i--)
            {
                page.Opacity = i / 100d;

                await Task.Delay(Time * 10);
            }

            page.Visibility = Visibility.Hidden;

            page.Opacity = 1;
        }

        public static async void Hide(this Page page)
        {
            for (int i = 0; i < 99; i++)
            {
                page.Opacity = i / 100d;

                await Task.Delay(Time * 10);
            }

            page.Visibility = Visibility.Visible;

            page.Opacity = 0;
        }
    }
}

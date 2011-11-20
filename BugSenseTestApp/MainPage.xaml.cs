using System;
using System.Threading;
using System.Windows;
using BugSense;
using ExternalLibrary;

namespace BugSenseTestApp {
    public partial class MainPage {
        // Constructor
        public MainPage()
        {
            InitializeComponent();
        }
        private void OnThrowExClick(object sender, RoutedEventArgs e)
        {
            throw new NotSupportedException("The feature you are trying to access is currently not supported.");
        }

        private void OnAsyncClick(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(state => {
                Thread.Sleep(5000);
                try {
                    throw new InvalidOperationException("An exception occured while executing in a different thread.");
                }
                catch (Exception ex) {
                    var overridenOptions = BugSenseHandler.DefaultOptions();
                    overridenOptions.Text = ex.Message + Environment.NewLine + "Do you want to send the Exception?";
                    overridenOptions.Type = enNotificationType.MessageBoxConfirm;
                    BugSenseHandler.HandleError(ex, string.Format("Happend at {0}", DateTime.Now), options: overridenOptions);
                }
            });
        }

        private void OnOtherLibClick(object sender, RoutedEventArgs e)
        {
            Db d = new Db();
            d.LoadTables();
        }

        private void OnUnhandledExceptionClick(object sender, RoutedEventArgs e)
        {
            throw new BugSenseUnhandledException();
        }
    }
}
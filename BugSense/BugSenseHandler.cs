using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows;
using BugSense.Coroutines;
using BugSense.Extensions;
using BugSense.Internal;
using BugSense.Tasks;
using Microsoft.Phone.Info;
using Microsoft.Phone.Net.NetworkInformation;
using Microsoft.Phone.Reactive;
using BugSense.Notifications;

namespace BugSense {
    public sealed class BugSenseHandler {

        #region [ Singleton ]

        BugSenseHandler()
        {

        }

        public static BugSenseHandler Instance
        {
            get
            {
                return Nested.instance;
            }
        }

        class Nested {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested()
            {
            }

            internal static readonly BugSenseHandler instance = new BugSenseHandler();
        }

        #endregion

        #region [ Fields ]

        private NotificationOptions _options;
        private Application _application;
        private bool _initialized;
        private string _appVersion;
        private string _appName;
        public event EventHandler<BugSenseUnhandledExceptionEventArgs> UnhandledException;
        /// <summary>
        /// Occurs when the unhandled exception is sent to BugSense
        /// </summary>
        //public event EventHandler<BugSenseLogErrorCompletedEventArgs> UnhandledExceptionSent;

        #endregion

        #region [ Public Methods ]

        /// <summary>
        /// Use this method inside a catch block or when you want to send error details sto BugSense
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="comment"></param>
        /// <param name="options"></param>
        [Obsolete("Use BugSenseHandler.Instance.LogError")]
        public static void HandleError(Exception ex, string comment = null, NotificationOptions options = null)
        {
            Instance.LogError(ex, comment, options);
        }

        public void LogError(Exception ex, string comment = null, NotificationOptions options = null)
        {
            if (!_initialized)
                throw new InvalidOperationException("BugSense Handler is not initialized.");
            Handle(ex, comment, options ?? Instance._options);
        }

        /// <summary>
        /// Initialized the BugSense handler. Must be called at App constructor.
        /// </summary>
        /// <param name="application">The Windows Phone application.</param>
        /// <param name="apiKey">The Api Key that can be retrieved at bugsense.com</param>
        /// <param name="options">Optional Options</param>
        public void Init(Application application, string apiKey, NotificationOptions options = null)
        {
            if (_initialized)
                return;

            //General Initializations
            _options = options ?? GetDefaultOptions();
            _application = application;
            G.API_KEY = apiKey;

            //Getting version and app details
            var appDetails = Helpers.GetVersion();
            _appName = appDetails[0];//nameHelper.Name;
            _appVersion = appDetails[1];//nameHelper.Version.ToString();

            //Attaching the handler
            _application.UnhandledException += OnUnhandledException;

            //Proccess errors from previous crashes
            var tasks = new List<IResult> { new ProccessErrorsTask() };
            Coroutine.BeginExecute(tasks.GetEnumerator());

            //Just in case Init is called again
            _initialized = true;
        }


        /// <summary>
        /// Gets default options for error handling
        /// </summary>
        /// <returns></returns>
        [Obsolete("Use BugSenseHandler.Instance.GetDefaultOptions")]
        public static NotificationOptions DefaultOptions()
        {
            return Instance.GetDefaultOptions();
        }

        public NotificationOptions GetDefaultOptions()
        {
            return new NotificationOptions {
                Title = Labels.DefaultNotificationTitle,
                Text = Labels.DefaultNotificationText_MessageBox,
                Type = enNotificationType.MessageBox
            };
        }

        #endregion

        #region [ Private Core Methods ]

        private void OnBugSenseUnhandledException(BugSenseUnhandledExceptionEventArgs e)
        {
            EventHandler<BugSenseUnhandledExceptionEventArgs> handler = UnhandledException;
            if (handler != null)
                handler(this, e);
        }

        //private void OnUnhandledExceptionSent(BugSenseLogErrorCompletedEventArgs args)
        //{
        //    var handler = UnhandledExceptionSent;
        //    if (handler != null)
        //        handler(this, args);
        //}

        private void OnUnhandledException(object sender, ApplicationUnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is BugSenseUnhandledException)
                return;
            args.Handled = true;
            var e = new BugSenseUnhandledExceptionEventArgs(args.ExceptionObject, args.Handled);
            OnBugSenseUnhandledException(e);
            args.Handled = e.Handled;
            if (e.Cancel)
                return;
            if (Debugger.IsAttached && !_options.HandleWhileDebugging)
                return;

            Handle(args.ExceptionObject, e.Comment, _options, !args.Handled);
            args.Handled = true;
        }

        private DateTime _lastMethodHandledCalledAt;

        private void Handle(Exception e, string comment, NotificationOptions options, bool throwExceptionAfterComplete = false)
        {
            if (DateTime.Now.AddSeconds(-1) < _lastMethodHandledCalledAt) {
                return;
            }
            _lastMethodHandledCalledAt = DateTime.Now;
            if (Debugger.IsAttached && !options.HandleWhileDebugging)//Dont send the error
                return;
            var request = new BugSenseRequest(e.ToBugSenseEx(comment), GetEnvironment());
            if (throwExceptionAfterComplete) {
                LogAndSend(request, true);
                return;
            }
            try {
                switch (options.Type) {
                    case enNotificationType.MessageBox:
                        if (!NotificationBox.IsOpen())
                            NotificationBox.Show(options.Title, options.Text, new NotificationBoxCommand(Labels.OkMessage, () => { }));
                        LogAndSend(request);
                        break;
                    case enNotificationType.MessageBoxConfirm:
                        if (!NotificationBox.IsOpen())
                            Scheduler.Dispatcher.Schedule(
                                () => {
                                    try {
                                        if (!NotificationBox.IsOpen())
                                            NotificationBox.Show(options.Title, options.Text,
                                                                 new NotificationBoxCommand(Labels.OkMessage, () => LogAndSend(request)),
                                                                 new NotificationBoxCommand(Labels.CancelMessage, () => { }));
                                    }
                                    catch { }
                                });
                        break;
                    default:
                        LogAndSend(request);
                        break;
                }
            }
            catch (Exception) {
                if (options.Type != enNotificationType.MessageBoxConfirm) {
                    LogAndSend(request);
                }
            }
        }

        private void LogAndSend(BugSenseRequest request, bool throwAfterComplete = false)
        {
            ThreadPool.QueueUserWorkItem(state => {
                var eventArgs = new BugSenseLogErrorCompletedEventArgs(request, request.Exception != null ? request.Exception.OriginalException : null);
                //eventArgs.ExitApp = throwAfterComplete;
                var logTask = new LogErrorTask(request);
                var sendTask = new SendErrorTask();
                var tasks = new List<IResult> { logTask, sendTask };
                EventHandler<ResultCompletionEventArgs> callback = (sender, args) => Scheduler.Dispatcher.Schedule(() => {
                    //OnUnhandledExceptionSent(eventArgs);
                    //if (eventArgs.ExitApp)
                    throw new BugSenseUnhandledException();
                });
                Coroutine.BeginExecute(tasks.GetEnumerator(), callback: throwAfterComplete ? callback : null);
            });
        }

        #endregion

        #region [ Private Helper Methods ]

        private AppEnvironment GetEnvironment()
        {
            AppEnvironment environment = new AppEnvironment();
            environment.AppName = _appName;
            environment.AppVersion = _appVersion;
            environment.OsVersion = Environment.OSVersion.Version.ToString();
            string result = string.Empty;
            object manufacturer;
            if (DeviceExtendedProperties.TryGetValue("DeviceManufacturer", out manufacturer))
                result = manufacturer.ToString();
            object theModel;
            if (DeviceExtendedProperties.TryGetValue("DeviceName", out theModel))
                result = result + theModel;

            environment.PhoneModel = result;
            try {
                environment.ScreenHeight = _application.RootVisual.RenderSize.Height;
                environment.ScreenWidth = _application.RootVisual.RenderSize.Width;
            }
            catch { /* If the exception is not in the UIThread we don't have access to above */ }

            environment.GpsOn = "unavailable";
            environment.ScreenDpi = "unavailable";
            environment.ScreenOrientation = "unavailable";
            environment.WifiOn = NetworkInterface.NetworkInterfaceType.ToString();
            return environment;
        }

        #endregion

    }
}
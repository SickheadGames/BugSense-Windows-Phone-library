using System;
using System.Windows;
using System.Windows.Controls;
using System.IO.IsolatedStorage;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using Microsoft.Phone.Controls;
using System.ComponentModel;

namespace BugSense.Notifications {
    /// <summary>
    /// Represents a message-box like control for displaying a message box
    /// with custom actions an option to persist 'show message again' state. Original can be found here: http://shareourideas.com/2011/03/05/custom-message-box-in-windows-phone-7/
    /// </summary>
    public class NotificationBox : Control {
        #region Fields

        private static Popup _popup;
        private static bool _appBarVisibility;

        #endregion

        #region Properties

        #region CommandsSource Property

        /// <summary>
        /// Gets or sets a collection of <see cref="NotificationBoxCommand"/>.
        /// </summary>
        public IEnumerable<NotificationBoxCommand> CommandsSource
        {
            get { return (IEnumerable<NotificationBoxCommand>)GetValue(CommandsSourceProperty); }
            set { SetValue(CommandsSourceProperty, value); }
        }

        /// <value>Identifies the CommandsSource dependency property</value>
        public static readonly DependencyProperty CommandsSourceProperty =
            DependencyProperty.Register(
            "CommandsSource",
            typeof(IEnumerable<NotificationBoxCommand>),
            typeof(NotificationBox),
              new PropertyMetadata(default(IEnumerable<NotificationBoxCommand>), CommandsSourceChanged));

        private static void CommandsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var newCommands = e.NewValue as IEnumerable<NotificationBoxCommand>;
            if (newCommands != null) {
                var box = d as NotificationBox;
                foreach (var newCommand in newCommands) {
                    newCommand.Owner = box;
                }
            }
        }

        #endregion

        #region Title Property

        /// <summary>
        /// Gets or sets the title text to display.
        /// </summary>
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        /// <value>Identifies the Title dependency property</value>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
            "Title",
            typeof(string),
            typeof(NotificationBox),
              new PropertyMetadata(default(string)));

        #endregion

        #region Message Property

        /// <summary>
        /// Gets or sets the message text to display.
        /// </summary>
        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        /// <value>Identifies the Message dependency property</value>
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
            "Message",
            typeof(string),
            typeof(NotificationBox),
              new PropertyMetadata(default(string)));

        #endregion

        private static IsolatedStorageSettings Settings
        {
            get { return IsolatedStorageSettings.ApplicationSettings; }
        }

        #endregion

        #region Ctor

        protected NotificationBox()
        {
            DefaultStyleKey = typeof(NotificationBox);
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Displays a notification box with title, message and custom actions.
        /// </summary>
        /// <param name="title">The title of this message.</param>
        /// <param name="message">The message body text.</param>
        /// <param name="commands">A collection of actions.</param>
        public static void Show(string title, string message, params NotificationBoxCommand[] commands)
        {
            if (_popup != null) {
                throw new InvalidOperationException("Message is already shown.");
            }

            HandleBackKeyAndAppBar();
            _popup = new Popup {
                IsOpen = true,
                Child = new NotificationBox {
                    Width = CurrentPage.ActualWidth,
                    Height = CurrentPage.ActualHeight + 40,
                    Title = title,
                    Message = message,
                    CommandsSource = commands
                }
            };            
        }

        internal void Close()
        {
            ClosePopup();
        }

        public static bool IsOpen()
        {
            return _popup != null && _popup.IsOpen;
        }

        #endregion

        #region Privates

        private static PhoneApplicationPage CurrentPage
        {
            get
            {
                var rootFrame = Application.Current.RootVisual as PhoneApplicationFrame;
                var currentPage = rootFrame.Content as PhoneApplicationPage;
                return currentPage;
            }
        }

        private static void HandleBackKeyAndAppBar()
        {
            CurrentPage.BackKeyPress += parentPage_BackKeyPress;
            if (CurrentPage.ApplicationBar != null) {
                _appBarVisibility = CurrentPage.ApplicationBar.IsVisible;
                CurrentPage.ApplicationBar.IsVisible = false;
            }
        }

        private static void parentPage_BackKeyPress(object sender, CancelEventArgs e)
        {
            CurrentPage.BackKeyPress -= parentPage_BackKeyPress;
            ClosePopup();

            e.Cancel = true;
        }

        private static void ClosePopup()
        {
            if (_popup != null) {
                _popup.IsOpen = false;
                _popup = null;
            }

            if (CurrentPage.ApplicationBar != null) {
                CurrentPage.ApplicationBar.IsVisible = _appBarVisibility;
            }
        }

        #endregion
    }
}

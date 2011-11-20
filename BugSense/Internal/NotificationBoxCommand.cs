using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace BugSense.Notifications
{
    /// <summary>
    /// Represents a <see cref="NotificationBox"/> command.
    /// </summary>
    public class NotificationBoxCommand : ICommand
    {
        #region Fields

        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        
        #endregion

        #region Properties

        public object Content { get; private set; }

        internal NotificationBox Owner { get; set; }

        #region Command Attached Property
        public static NotificationBoxCommand GetCommand(DependencyObject obj)
        {
            return (NotificationBoxCommand)obj.GetValue(CommandProperty);
        }

        public static void SetCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(CommandProperty, value);
        }

        // Using a DependencyProperty as the backing store for Command.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached(
                "Command",
                typeof(NotificationBoxCommand),
                typeof(NotificationBoxCommand),
                new PropertyMetadata(null, CommandChanged)); 

        private static void CommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = d as ButtonBase;
            if (button == null)
            {
                throw new ArgumentException("The NotificationBoxCommand.CommandProperty attached property is valid on ButtonBase or derived types only.");
            }

            var oldCommand = e.OldValue as ICommand;
            if (oldCommand == null)
            {
                // First time initialized. Register click event only once.
                button.Click += button_Click;

                // Track when button unloads from visual tree for unregistering.
                button.Unloaded += button_Unloaded;
            }            
        }

        #endregion

        #endregion

        private static void button_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ButtonBase;
            var command = GetCommand(button);
            if (command != null && command.CanExecute(null))
            {
                command.Execute(null);
            }

            command.Owner.Close();
        }

        private static void button_Unloaded(object sender, RoutedEventArgs e)
        {
            var button = sender as ButtonBase;
            button.Unloaded -= button_Unloaded;
            button.Click -= button_Click;
        }

        #region Ctor

        public NotificationBoxCommand(object content, Action execute, Func<bool> canExecute)
        {
            Content = content;
            _execute = execute;
            _canExecute = canExecute;
        }

        public NotificationBoxCommand(object content, Action execute)
        {
            Content = content;
            _execute = execute;
            _canExecute = () => true;
        }

        #endregion

        public bool CanExecute(object parameter)
        {
            return _canExecute();
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter)
        {
            _execute();
        }

        public void Invalidate()
        {
            if (CanExecuteChanged != null)
            {
                CanExecuteChanged(this, EventArgs.Empty);
            }
        }
    }
}

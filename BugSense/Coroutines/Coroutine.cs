using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace BugSense.Coroutines {
    /// <summary>
    /// Manages coroutine execution. This is a fork from Caliburn Micro
    /// </summary>
    internal static class Coroutine {
        //static readonly ILog Log = LogManager.GetLog(typeof(Coroutine));

        /// <summary>
        /// Creates the parent enumerator.
        /// </summary>
        public static Func<IEnumerator<IResult>, IResult> CreateParentEnumerator = inner => new SequentialResult(inner);

        /// <summary>
        /// Executes a coroutine.
        /// </summary>
        /// <param name="coroutine">The coroutine to execute.</param>
        /// <param name="context">The context to execute the coroutine within.</param>
        /// /// <param name="callback">The completion callback for the coroutine.</param>
        public static void BeginExecute(IEnumerator<IResult> coroutine, ActionExecutionContext context = null, EventHandler<ResultCompletionEventArgs> callback = null)
        {
            //Log.Info("Executing coroutine.");

            var enumerator = CreateParentEnumerator(coroutine);
            //IoC.BuildUp(enumerator);

            if (callback != null)
                enumerator.Completed += callback;
            enumerator.Completed += Completed;

            enumerator.Execute(context ?? new ActionExecutionContext());
        }

        /// <summary>
        /// Called upon completion of a coroutine.
        /// </summary>
        public static event EventHandler<ResultCompletionEventArgs> Completed = (s, e) => {
            var enumerator = (IResult)s;
            enumerator.Completed -= Completed;

            //if (e.Error != null)
            //    Log.Error(e.Error);
            //else if (e.WasCancelled)
            //    Log.Info("Coroutine execution cancelled.");
            //else
            //    Log.Info("Coroutine execution completed.");
        };
    }
}

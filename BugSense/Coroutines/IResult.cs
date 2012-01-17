using System;


namespace BugSense.Coroutines {

    /// <summary>
    /// Allows custom code to execute after the return of a action.
    /// </summary>
    internal interface IResult {
        /// <summary>
        /// Executes the result using the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        void Execute(ActionExecutionContext context);

        /// <summary>
        /// Occurs when execution has completed.
        /// </summary>
        event EventHandler<ResultCompletionEventArgs> Completed;
    }
}

using System.Threading.Tasks;

namespace CustomSession
{
    internal static class ValueTaskExtensions
    {
        // If the ValueTask<T> is already completed synchronously, produce a completed ValueTask<T>
        // with the result so callers can avoid an extra await allocation.
        public static ValueTask<T> EnsureValueTaskResult<T>(this ValueTask<T> valueTask)
        {
            var awaiter = valueTask.GetAwaiter();
            if (awaiter.IsCompleted)
            {
                // GetResult will throw if the task faulted — preserve that behavior.
                T result = awaiter.GetResult();
                return new ValueTask<T>(result);
            }

            return valueTask;
        }

        // For non-generic ValueTask, return a completed ValueTask if already completed.
        public static ValueTask EnsureValueTaskCompleted(this ValueTask valueTask)
        {
            var awaiter = valueTask.GetAwaiter();
            if (awaiter.IsCompleted)
            {
                awaiter.GetResult();
                return new ValueTask();
            }

            return valueTask;
        }
    }
}

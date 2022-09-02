namespace Jamq.Client.Rabbit.Consuming;

internal static class CountdownEventSafeExtensions
{
    public static void SafeSignal(this CountdownEvent countdownEvent)
    {
        try
        {
            countdownEvent.Signal();
        }
        catch
        {
            // ignored
        }
    }

    public static bool SafeIncrement(this CountdownEvent countdownEvent)
    {
        try
        {
            return countdownEvent.TryAddCount(1);
        }
        catch
        {
            return false;
        }
    }
}
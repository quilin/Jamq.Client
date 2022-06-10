namespace RMQ.Client.Connection.Adapters;

internal enum ConnectionBusinessStatus
{
    Idle = 0,
    Free = 1,
    Working = 2,
    Busy = 3,
    Full = 4
}
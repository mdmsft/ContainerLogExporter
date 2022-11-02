using Microsoft.Extensions.Logging;

namespace ContainerLogExporter;

internal static class Events
{
    public static readonly EventId MessageIsNullOrEmpty = 1000;
    public static readonly EventId MessageCannotBeDeserialized = 1001;
    public static readonly EventId WorkspaceKeyNotFound = 2000;
    public static readonly EventId WorkspaceSendLogs = 2000;
}
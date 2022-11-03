using Microsoft.Extensions.Logging;

namespace ContainerLogExporter;

internal static class Events
{
    public static readonly EventId MessageIsNullOrEmpty = 1000;
    public static readonly EventId MessageCannotBeDeserialized = 1001;

    public static readonly EventId WorkspaceLookup = 2000;
    public static readonly EventId WorkspaceCacheMiss = 2001;
    public static readonly EventId WorkspaceFound = 2002;
    public static readonly EventId WorkspaceNotFound = 2003;
    public static readonly EventId WorkspaceSendLogs = 2004;
}
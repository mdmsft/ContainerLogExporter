using Microsoft.Extensions.Logging;

namespace ContainerLogExporter;

internal static class Events
{
    public static readonly EventId MessageIsEmpty = 1000;
    public static readonly EventId MessageCannotBeParsed = 1001;
    public static readonly EventId RecordsFound = 1002;

    public static readonly EventId WorkspaceDataLookup = 2000;
    public static readonly EventId WorkspaceDataNotFound = 2001;
    public static readonly EventId WorkspaceDataFound = 2002;
    public static readonly EventId WorkspaceSendLogs = 2003;
    public static readonly EventId WorkspaceSendLogsHttpError = 2004;
}
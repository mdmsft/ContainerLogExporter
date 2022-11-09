using Microsoft.Extensions.Logging;

namespace ContainerLogExporter;

internal static class Events
{
    public static readonly EventId MessageIsEmpty = 1000;
    public static readonly EventId MessageCannotBeParsed = 1001;
    public static readonly EventId RecordsFound = 1002;

    public static readonly EventId WorkspaceIdLookup = 2000;
    public static readonly EventId WorkspaceIdNotFound = 2001;
    public static readonly EventId WorkspaceIdFound = 2002;
    public static readonly EventId WorkspaceKeyLookup = 2003;
    public static readonly EventId WorkspaceKeyNotFound = 2004;
    public static readonly EventId WorkspaceKeyFound = 2005;
    public static readonly EventId WorkspaceSendLogs = 2006;
    public static readonly EventId WorkspaceSendLogsHttpError = 2007;
}
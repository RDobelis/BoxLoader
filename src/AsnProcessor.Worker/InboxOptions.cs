namespace AsnProcessor.Worker;

public class InboxOptions
{
    public string DataRoot { get; set; } = "data";
    public string InboxFolder { get; set; } = "inbox";
    public string ArchiveFolder { get; set; } = "archive";
    public string FailedFolder { get; set; } = "failed";

    public string InboxPath => Path.Combine(DataRoot, InboxFolder);
    public string ArchivePath => Path.Combine(DataRoot, ArchiveFolder);
    public string FailedPath => Path.Combine(DataRoot, FailedFolder);
}
namespace NpuTools.Organize.Models;

internal sealed record RenameProposal(string OriginalPath, string ProposedPath)
{
    public string OriginalName => System.IO.Path.GetFileName(OriginalPath);
    public string ProposedName => System.IO.Path.GetFileName(ProposedPath);
}

using System.Collections.Generic;

namespace MapEditorTool.Executor.RuntimeVerify
{
    public sealed class MapRuntimeVerificationReport
    {
        public MapRuntimeVerificationReport()
        {
            ProjectRoot = string.Empty;
            GeneratedAtUtc = string.Empty;
            VerificationKind = string.Empty;
            ProofScope = string.Empty;
            Checks = new List<MapRuntimeCheck>();
            EntryRooms = new List<MapRuntimeEntryRoom>();
            PortalTargets = new List<MapRuntimePortalTarget>();
            Issues = new List<string>();
        }

        public string ProjectRoot { get; set; }
        public bool ProjectFileExists { get; set; }
        public string GeneratedAtUtc { get; set; }
        public string VerificationKind { get; set; }
        public string ProofScope { get; set; }
        public int MapCount { get; set; }
        public int PortalCount { get; set; }
        public int LinkCount { get; set; }
        public int PortalTargetCount { get; set; }
        public int ResolvedPortalTargetCount { get; set; }
        public int EntryRoomCount { get; set; }
        public int ResolvedEntryRoomCount { get; set; }
        public int CheckCount { get; set; }
        public int IssueCount { get; set; }
        public bool Ok { get; set; }
        public List<MapRuntimeCheck> Checks { get; set; }
        public List<MapRuntimeEntryRoom> EntryRooms { get; set; }
        public List<MapRuntimePortalTarget> PortalTargets { get; set; }
        public List<string> Issues { get; set; }
    }

    public sealed class MapRuntimeCheck
    {
        public MapRuntimeCheck()
        {
            Id = string.Empty;
            Path = string.Empty;
            Detail = string.Empty;
        }

        public string Id { get; set; }
        public bool Passed { get; set; }
        public string Path { get; set; }
        public string Detail { get; set; }
    }

    public sealed class MapRuntimeEntryRoom
    {
        public MapRuntimeEntryRoom()
        {
            Source = string.Empty;
            RawValue = string.Empty;
            ResolvedPath = string.Empty;
        }

        public string Source { get; set; }
        public string RawValue { get; set; }
        public string ResolvedPath { get; set; }
        public bool Exists { get; set; }
        public bool InImportedMapGraph { get; set; }
    }

    public sealed class MapRuntimePortalTarget
    {
        public MapRuntimePortalTarget()
        {
            FromMapPath = string.Empty;
            PortalId = string.Empty;
            PortalName = string.Empty;
            RawTargetMap = string.Empty;
            ResolvedTargetMap = string.Empty;
        }

        public string FromMapPath { get; set; }
        public string PortalId { get; set; }
        public string PortalName { get; set; }
        public string RawTargetMap { get; set; }
        public string ResolvedTargetMap { get; set; }
        public bool ResolvesToImportedMap { get; set; }
    }
}

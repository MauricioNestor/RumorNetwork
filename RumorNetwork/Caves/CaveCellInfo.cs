namespace RumorNetwork.Caves
{
    public sealed class CaveCellInfo
    {
        public CaveTraversalKind Traversal { get; }

        public CaveMedium Medium { get; }

        public bool IsTraversable =>
            Traversal == CaveTraversalKind.Open ||
            Traversal == CaveTraversalKind.Openable;

        public CaveCellInfo(
            CaveTraversalKind traversal,
            CaveMedium medium
        )
        {
            Traversal = traversal;
            Medium = medium;
        }
    }
}

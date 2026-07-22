using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RumorNetwork.Commands
{
    public sealed class StructureInspectionState
    {
        private readonly List<GeneratedStructure> structures = new();

        public IReadOnlyList<GeneratedStructure> Structures => structures;

        public int Count => structures.Count;

        public string Filter { get; private set; } = string.Empty;

        public void Replace(
            string filter,
            IEnumerable<GeneratedStructure> inspectedStructures
        )
        {
            Filter = filter;
            structures.Clear();
            structures.AddRange(inspectedStructures);
        }

        public bool TryGet(
            int index,
            out GeneratedStructure? structure
        )
        {
            if (index < 0 || index >= structures.Count)
            {
                structure = null;
                return false;
            }

            structure = structures[index];
            return true;
        }
    }
}

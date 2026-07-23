using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RumorNetwork.Dialogue
{
    internal static class VariablesModSystemExtensions
    {
        public static void SetVariable(
            this VariablesModSystem variables,
            EnumActivityVariableScope scope,
            Entity callingEntity,
            string name,
            string value
        )
        {
            variables.SetVariable(
                callingEntity,
                scope,
                name,
                value
            );
        }
    }
}
